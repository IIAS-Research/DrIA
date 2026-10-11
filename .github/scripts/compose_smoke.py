#!/usr/bin/env python3
"""Verify the deployed Compose stack and its data after container recreation."""

import argparse
import base64
import hashlib
import importlib.util
import json
import math
import os
import re
import socket
import ssl
import struct
import time
import urllib.parse
import urllib.request
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("verify_iaml", ROOT / "scripts" / "verify-iaml.py")
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)
Api, dataset, study, wait_for = VERIFY.Api, VERIFY.dataset, VERIFY.study, VERIFY.wait_for
TERMINAL = {"FINISHED", "CACHED", "CRASHED", "TIMEOUT", "CANCELED"}


def check_frontend(base_url, project_id=None):
    paths = ["/"] + ([f"/projets/{project_id}"] if project_id else [])
    for path in paths:
        with urllib.request.urlopen(base_url + path, timeout=30) as response:
            html = response.read().decode()
            assert response.status == 200 and "text/html" in response.headers.get("Content-Type", "")
            assert "<html" in html.lower(), "The frontend did not return an HTML page"
    script = re.search(r'<script\b[^>]*\bsrc=["\']([^"\']+)["\']', html)
    assert script, "The frontend bundle is missing"
    asset_url = urllib.parse.urljoin(base_url + "/", script.group(1))
    assert urllib.parse.urlsplit(asset_url).netloc == urllib.parse.urlsplit(base_url).netloc
    with urllib.request.urlopen(asset_url, timeout=30) as response:
        assert response.status == 200 and response.read(), "The frontend bundle could not be loaded"
        assert "javascript" in response.headers.get("Content-Type", ""), "The bundle URL returned the SPA HTML instead of JavaScript"


def check_websocket(base_url, token, job_id):
    endpoint = urllib.parse.urlsplit(base_url)
    path = endpoint.path.rstrip("/") + "/api/ws/generateModel"
    port = endpoint.port or (443 if endpoint.scheme == "https" else 80)
    connection = socket.create_connection((endpoint.hostname, port), timeout=15)
    if endpoint.scheme == "https":
        connection = ssl.create_default_context().wrap_socket(connection, server_hostname=endpoint.hostname)
    with connection:
        key = base64.b64encode(os.urandom(16)).decode()
        request = (f"GET {path} HTTP/1.1\r\nHost: {endpoint.netloc}\r\n"
                   f"Authorization: Bearer {token}\r\n"
                   f"Origin: {base_url}\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                   f"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n")
        connection.sendall(request.encode())
        headers = bytearray()
        while not headers.endswith(b"\r\n\r\n"):
            part = connection.recv(1)
            assert part and len(headers) < 16384, "The WebSocket upgrade did not complete"
            headers.extend(part)
        lines = headers.decode().split("\r\n")
        assert lines[0].split()[1] == "101", "The proxy did not upgrade the authenticated WebSocket"
        values = {name.lower(): value.strip() for name, value in (line.split(":", 1) for line in lines[1:] if ":" in line)}
        expected = base64.b64encode(hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest()).decode()
        assert values["sec-websocket-accept"] == expected

        def send(payload, opcode=1):
            mask = os.urandom(4)
            size = len(payload)
            length = bytes([size | 128]) if size < 126 else bytes([126 | 128]) + struct.pack("!H", size)
            masked = bytes(value ^ mask[index % 4] for index, value in enumerate(payload))
            connection.sendall(bytes([128 | opcode]) + length + mask + masked)

        def read(size):
            result = bytearray()
            while len(result) < size:
                part = connection.recv(size - len(result))
                assert part, "The WebSocket disconnected before the SignalR response"
                result.extend(part)
            return bytes(result)

        def messages():
            buffer = bytearray()
            deadline = time.monotonic() + 15
            while time.monotonic() < deadline:
                connection.settimeout(max(0.1, deadline - time.monotonic()))
                first, second = read(2)
                opcode, size = first & 15, second & 127
                if size == 126:
                    size = struct.unpack("!H", read(2))[0]
                elif size == 127:
                    size = struct.unpack("!Q", read(8))[0]
                assert size <= 1048576, "Unexpected WebSocket frame size"
                mask = read(4) if second & 128 else None
                payload = read(size)
                if mask:
                    payload = bytes(value ^ mask[index % 4] for index, value in enumerate(payload))
                if opcode == 9:
                    send(payload, opcode=10)
                    continue
                assert opcode != 8, "The WebSocket closed before the SignalR response"
                if opcode in {0, 1}:
                    buffer.extend(payload)
                    while b"\x1e" in buffer:
                        record, _, buffer = buffer.partition(b"\x1e")
                        yield json.loads(record)
            raise AssertionError("Timed out waiting for a SignalR response")

        incoming = messages()
        send(b'{"protocol":"json","version":1}\x1e')
        assert next(incoming) == {}, "The SignalR JSON handshake failed"
        send(json.dumps({"type": 1, "invocationId": "smoke", "target": "JoinGroup", "arguments": [job_id]}).encode() + b"\x1e")
        for message in incoming:
            if message.get("type") == 3 and message.get("invocationId") == "smoke":
                assert "error" not in message, "SignalR rejected the owned training job"
                break
        send(b"", opcode=8)


def create_project(api):
    _, content = dataset("regression")
    configuration = study("regression")
    configuration["options"].update(max_duration=60, max_workers=4)
    configuration["explanations"]["children"] = []
    configuration = api.request("/IaWebService/validate", "POST", configuration)["configuration"]
    assert configuration["schema_version"] == 1 and configuration["options"]["max_duration"] == 60
    fields = {
        "title": "Compose smoke " + uuid.uuid4().hex[:8],
        "description": "Synthetic deployment verification",
        "csvColumnToPredict": "target",
        "rowDepedencies": "true",
        "rowDepedenciesLinkedString": '["group"]',
        "datasetHasIdColumn": "true",
        "datasetHasIdColumnListString": '["patient_id"]',
        "pipeline": json.dumps(configuration),
        "survivalModel": "false",
        "evaluationMode": "split",
        "creationContextJson": '{"predictionKind":"quantity"}',
    }
    started = api.request("/Projects", "POST", fields=fields, files=[("file", "synthetic.csv", content)], expected=202)
    assert started["maxDuration"] == 60 and isinstance(started["id"], str)
    return {"project_id": started["projectId"], "process_id": started["processId"], "remote_job_id": started["id"]}


def check_persistence(api, state, timeout):
    def persisted(project):
        process = next((item for item in project["processes"] if item["id"] == state["process_id"]), None)
        baseline = project.get("metricsBaseline") or {}
        assert baseline.get("status", {}).get("current") not in {"CRASHED", "TIMEOUT", "CANCELED"}, "The baseline training failed"
        return process and process.get("models") and (process.get("statusSnapshot") or {}).get("results_persisted") and baseline.get("metrics")

    project = wait_for(lambda: api.request(f"/Projects/{state['project_id']}/nested-data"), persisted, "persisted baseline and candidate", timeout)
    process = next(item for item in project["processes"] if item["id"] == state["process_id"])
    model = next((item for item in process["models"] if item["id"] == state.get("model_id")), None) if "model_id" in state else process["models"][0]
    assert model, "The persisted model is missing after recreation"
    baseline = project["metricsBaseline"]["metrics"][0]
    for metrics in [baseline, model["metrics"]]:
        assert metrics["objective"]["key"] == "objective" and metrics["objective"]["greater_is_better"] is False
        for scores in [metrics["metrics_train"], metrics["metrics_test"]]:
            assert isinstance(scores["objective"], (int, float)) and math.isfinite(scores["objective"]), "A persisted score is missing"
    assert baseline["score_source"] == "in_sample" and model["metrics"]["score_source"] == "cross_validation"
    state["model_id"] = model["id"]
    row = dataset("regression")[0][0]
    prediction = api.request(f"/Models/{model['id']}/predict", "POST", {"json_row": [{"age": row["age"], "marker": row["marker"]}], "method": "predict"})["prediction"]
    assert len(prediction) == 1 and math.isclose(prediction[0], row["target"], abs_tol=1e-4), "The synthetic regression prediction is incorrect"
    artifact = api.request(f"/Models/{model['id']}/download")
    assert isinstance(artifact, bytes) and len(artifact) > 100, "The saved model artifact is missing"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://127.0.0.1:8080")
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--after-restart", action="store_true")
    parser.add_argument("--timeout", type=int, default=180, choices=range(1, 181), metavar="1..180")
    args = parser.parse_args()
    email, password = os.environ.get("DRIA_EMAIL"), os.environ.get("DRIA_PASSWORD")
    if not email or not password:
        parser.error("DRIA_EMAIL and DRIA_PASSWORD must be set")
    base_url = args.base_url.rstrip("/")
    api = Api(base_url + "/api")
    wait_for(lambda: api.request("/health"), lambda health: health.get("status") == "ok", "the DrIA API", args.timeout)
    api.token = api.request("/Auth/login", "POST", {"email": email, "password": password})["accessToken"]
    health = api.request("/IaWebService/health")
    assert health["status"] == "ok" and health["schema_version"] == 1
    check_frontend(base_url)
    if args.after_restart:
        state = json.loads(args.state.read_text())
    else:
        state = create_project(api)
        args.state.parent.mkdir(parents=True, exist_ok=True)
        args.state.write_text(json.dumps(state, indent=2) + "\n")
        check_websocket(base_url, api.token, state["remote_job_id"])
        status = wait_for(lambda: api.request(f"/Processes/{state['process_id']}/status"), lambda value: value["status"]["current"] in TERMINAL, "regression training", args.timeout)
        assert status["status"]["current"] in {"FINISHED", "CACHED"}, "The regression training did not succeed"
    check_persistence(api, state, args.timeout)
    check_frontend(base_url, state["project_id"])
    args.state.write_text(json.dumps(state, indent=2) + "\n")
    print("PASS " + ("container recreation and persisted results" if args.after_restart else "Compose frontend, API, WebSocket, training and prediction"), flush=True)


if __name__ == "__main__":
    main()
