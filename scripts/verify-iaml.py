#!/usr/bin/env python3
"""Exercise the real Dr IA / IAML integration with synthetic data.

Requires running Dr IA, PostgreSQL and IAML. Only projects created by this run
are removed; --keep-projects leaves examples available for browser testing.
No external Python packages are needed. Credentials can be supplied through
DRIA_EMAIL and DRIA_PASSWORD rather than command-line arguments.
"""

import argparse
import copy
import csv
import io
import json
import os
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path


class ApiError(AssertionError):
    def __init__(self, message, status):
        super().__init__(message)
        self.status = status


class Api:
    def __init__(self, url):
        self.url = url.rstrip("/")
        self.token = None

    def request(self, path, method="GET", payload=None, fields=None, files=None, expected=200):
        headers = {}
        body = None
        if self.token:
            headers["Authorization"] = "Bearer " + self.token
        if fields is not None or files is not None:
            boundary = "dria-verification-" + uuid.uuid4().hex
            chunks = []
            for name, value in (fields or {}).items():
                chunks.append((f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n').encode())
            for name, filename, content in files or []:
                chunks.append((f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n').encode() + content + b"\r\n")
            chunks.append(f"--{boundary}--\r\n".encode())
            body = b"".join(chunks)
            headers["Content-Type"] = "multipart/form-data; boundary=" + boundary
        elif payload is not None:
            body = json.dumps(payload).encode()
            headers["Content-Type"] = "application/json"
        request = urllib.request.Request(self.url + path, data=body, headers=headers, method=method)
        try:
            response = urllib.request.urlopen(request, timeout=180)
        except urllib.error.HTTPError as error:
            response = error
        data = response.read()
        if response.status != expected:
            raise ApiError(f"{method} {path}: HTTP {response.status}, expected {expected}: {data[:1400].decode(errors='replace')}", response.status)
        if "json" in response.headers.get("Content-Type", ""):
            return json.loads(data)
        return data


def study(task):
    component, metric = {
        "classification": ("ActLogisticRegression", "BalancedAccuracyMetric"),
        "regression": ("ActLinearRegression", "MeanSquaredErrorMetric"),
        "survival": ("ActCox", "ConcordanceIndexMetric"),
    }[task]
    analyses = []
    if task == "classification":
        analyses = [
            {"type": "use", "component": "KernelSHAP", "alias": "importance", "params": {"nsamples": 20}},
            {"type": "use", "component": "ConfusionMatrixPlot", "alias": "performance"},
        ]
    elif task == "regression":
        analyses = [{"type": "use", "component": "ResidualsPlot", "alias": "residuals"}]
    return {
        "schema_version": 1,
        "pipeline": {"type": "sequence", "children": [{"type": "use", "component": component, "alias": "model"}]},
        "metrics": {"type": "collection", "kind": "metrics", "children": [{"type": "use", "component": metric, "alias": "objective"}]},
        "statistics": {"type": "collection", "kind": "statistics", "children": [{"type": "use", "component": "MeanStatistic", "alias": "mean"}, {"type": "use", "component": "CountStatistic", "alias": "count"}]},
        "explanations": {"type": "collection", "kind": "explanations", "children": analyses},
        "options": {"max_duration": 180, "max_workers": 1, "main_metric": "objective", "keep_training_history": True, "splitter": {"id": "kfold", "params": {"nb_folds": 3}}},
        "fit_options": {"n_candidates": 1, "patience": 0, "verbose": 0},
    }


def dataset(task):
    rows = []
    for i in range(80):
        age = 20 + i % 40
        marker = ((i * 13) % 100) / 10
        row = {"patient_id": f"synthetic-{i}", "group": i // 4, "age": age, "marker": marker, "target": i % 2}
        if task == "regression":
            row["target"] = 2 * age - 3 * marker + 1
        if task == "survival":
            row["target"] = "observed" if i % 2 else "censored"
            row["followup"] = 1 + age / 4 + marker
        rows.append(row)
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
    return rows, buffer.getvalue().encode()


def wait_for(fetch, ready, description, timeout):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            value = fetch()
        except (ApiError, urllib.error.URLError) as error:
            if isinstance(error, ApiError) and error.status != 503:
                raise
            time.sleep(2)
            continue
        if ready(value):
            return value
        time.sleep(1)
    raise AssertionError("Timed out waiting for " + description)


def check_configuration(api, iaml):
    health = api.request("/IaWebService/health")
    assert health["schema_version"] == 1 and health["status"] == "ok"
    catalogue = api.request("/IaWebService/catalogue")
    assert catalogue["components"] and catalogue["limits"]["min_duration"] == 60
    generated = api.request("/IaWebService/pipeline")
    original = iaml.request("/pipeline/generate")
    expected = copy.deepcopy(original)
    def recipe_nodes(node):
        if not isinstance(node, dict):
            return
        yield node
        for child in node.get("children", []):
            yield from recipe_nodes(child)
        yield from recipe_nodes(node.get("recipe"))
        for member in node.get("members", []):
            yield from recipe_nodes(member.get("recipe"))
    invalid_default = {"type": "parameter", "value": 1e-14, "domain": [1e-8, 1],
                       "categorical": None, "fixed": False, "optimizable": True,
                       "explicit_domain": False, "retained_initial": None}
    for node in recipe_nodes(expected.get("pipeline")):
        if node.get("component") == "ActGaussianProcessRegressor" and node.get("params", {}).get("alpha") == invalid_default:
            node["params"]["alpha"]["value"] = 1e-8
    assert generated == expected, "Only the invalid Gaussian registry default may change"
    resolved = api.request("/IaWebService/validate", "POST", original)["configuration"]
    assert resolved == generated, "Existing generated studies must receive the same correction"
    gaussian = next((node for node in recipe_nodes(original.get("pipeline"))
                     if node.get("type") == "use" and node.get("component") == "ActGaussianProcessRegressor"), None)
    if gaussian:
        for overrides in ({"value": 1e-6}, {"fixed": True},
                          {"domain": [1e-15, 1], "explicit_domain": True}):
            custom = study("regression")
            custom["pipeline"] = copy.deepcopy(gaussian)
            custom["pipeline"]["params"]["alpha"].update(overrides)
            actual = api.request("/IaWebService/validate", "POST", custom)["configuration"]
            assert actual["pipeline"]["params"]["alpha"] == custom["pipeline"]["params"]["alpha"], "Explicit Gaussian parameters must be preserved"
    assert {"pipeline", "metrics", "statistics", "explanations", "options", "fit_options"} <= generated.keys()
    validated = api.request("/IaWebService/validate", "POST", study("classification"))["configuration"]
    edited = api.request("/IaWebService/edit", "POST", {"configuration": validated, "operations": [{"op": "configure", "target": "model", "params": {"tol": 0.002}}]})["configuration"]
    diff = api.request("/IaWebService/diff", "POST", {"configuration": edited, "base": validated})
    assert "pipeline" in diff and diff["pipeline"]["changes"]
    exported = api.request("/IaWebService/export", "POST", edited)
    if isinstance(exported, bytes):
        exported = json.loads(exported)
    assert exported == edited
    python = api.request("/IaWebService/export?format=python", "POST", edited)
    assert b"create_study" in python
    invalid = api.request("/IaWebService/validate", "POST", {"pipeline": {}}, expected=400)
    assert invalid.get("error")
    rows, _ = dataset("classification")
    statistics = api.request("/IaWebService/statistics", "POST", {"train_dataset": [{k: v for k, v in row.items() if k != "patient_id"} for row in rows], "dataset_target": "target", "groups_columns": ["group"], "configuration": study("classification")})
    assert statistics["statistics"]["data"] and statistics["statistics"]["index"]
    assert statistics["plots"] and statistics.get("reports") is not None
    print("PASS configuration, catalogue, editing, diff, exports, pre-fit statistics and errors", flush=True)


def check_project(api, task, created_projects, timeout, existing=None):
    rows, content = dataset(task)
    configuration = (api.request(f"/Processes/{existing['process_id']}?projectId={existing['project_id']}") if existing
                     else api.request("/IaWebService/validate", "POST", study(task))["configuration"])
    fields = {"title": "IAML verification " + task + " " + uuid.uuid4().hex[:6], "description": "Synthetic integration verification", "csvColumnToPredict": "target", "rowDepedencies": "true", "rowDepedenciesLinkedString": '["group"]', "datasetHasIdColumn": "true", "datasetHasIdColumnListString": '["patient_id"]', "pipeline": json.dumps(configuration), "survivalModel": str(task == "survival").lower()}
    if task == "survival":
        fields.update(survivalModelDatasetTargetTime="followup", survivalModelEventValue=json.dumps("observed"))
    if existing:
        started = {"projectId": existing["project_id"], "processId": existing["process_id"], "id": existing["remote_job_id"], "maxDuration": configuration["options"]["max_duration"]}
    else:
        started = api.request("/Projects", "POST", fields=fields, files=[("file", task + ".csv", content)], expected=202)
        created_projects.append(started["projectId"])
    project_id, process_id = started["projectId"], started["processId"]
    assert isinstance(started["id"], str) and started["maxDuration"] == configuration["options"]["max_duration"]
    status = wait_for(lambda: api.request(f"/Processes/{process_id}/status"), lambda value: value["status"]["current"] in {"FINISHED", "CACHED", "CRASHED", "TIMEOUT", "CANCELED"}, task + " job", timeout)
    assert status["status"]["current"] in {"FINISHED", "CACHED"}, status
    project = wait_for(lambda: api.request(f"/Projects/{project_id}/nested-data"), lambda value: any(p["id"] == process_id and p.get("models") and (p.get("statusSnapshot") or {}).get("results_persisted") for p in value["processes"]) and (value.get("metricsBaseline") or {}).get("metrics"), task + " persisted candidates and baseline", timeout)
    process = next(p for p in project["processes"] if p["id"] == process_id)
    model = process["models"][0]
    model_id = model["id"]
    baseline = project["metricsBaseline"]
    assert baseline["status"]["current"] in {"FINISHED", "CACHED"}
    assert baseline["metrics"][0]["score_source"] == "in_sample"
    metrics = api.request(f"/Processes/{process_id}/metrics")
    candidate = metrics["metrics"][0]
    assert candidate["id"] == 0 and candidate["score_source"] == "cross_validation"
    assert candidate["objective"]["key"] == "objective"
    assert candidate["objective"]["greater_is_better"] is (task != "regression")
    assert candidate.get("plots") is None
    assert isinstance((candidate.get("metrics_test") or {}).get("objective"), (float, int)), "Independent test score was lost"
    saved_study = api.request(f"/Processes/{process_id}?projectId={project_id}")
    assert saved_study["schema_version"] == 1 and saved_study["options"]["max_duration"] == started["maxDuration"]
    assert saved_study["explanations"] == configuration["explanations"]
    assert api.request(f"/Models/{model_id}/download")
    assert api.request(f"/Models/{model_id}/report")
    assert api.request(f"/Models/{model_id}/audit")
    assert api.request(f"/Models/{model_id}/report?format=markdown")
    api.request(f"/Processes/{process_id}/history")
    api.request(f"/Processes/{process_id}/audit")
    features = [{"age": row["age"], "marker": row["marker"]} for row in rows[:2]]
    predicted = api.request(f"/Models/{model_id}/predict", "POST", {"json_row": features, "method": "predict"})
    assert len(predicted["prediction"]) == 2
    if task == "classification":
        probabilities = api.request(f"/Models/{model_id}/predict", "POST", {"json_row": features, "method": "predict_proba"})
        assert len(probabilities["prediction"]) == 2 and len(probabilities["prediction"][0]) == len(probabilities["classes"]) == 2
        decision = api.request(f"/Models/{model_id}/predict", "POST", {"json_row": features, "method": "decision_function"})
        assert len(decision["prediction"]) == 2
    if task == "survival":
        survival = api.request(f"/Models/{model_id}/predict", "POST", {"json_row": features, "method": "predict_survival_function", "times": [12, 14, 16]})
        assert len(survival["prediction"]) == 2 and survival["prediction"][0]["times"] == [12, 14, 16]
    else:
        scored = api.request(f"/Models/{model_id}/predict", "POST", {"json_row": features, "method": "score", "target": [row["target"] for row in rows[:2]]})
        assert isinstance(scored["prediction"], (float, int))
    transformed = api.request(f"/Models/{model_id}/predict", "POST", {"json_row": features, "method": "transform"})
    assert len(transformed["prediction"]) == 2
    unsupported = api.request(f"/Models/{model_id}/predict", "POST", {"json_row": features, "method": "unsupported_operation"}, expected=400)
    assert unsupported.get("error")
    evaluated = api.request(f"/Models/{model_id}/evaluate", "POST", fields={}, files=[("dataset", task + ".csv", content)])
    assert "objective" in evaluated["metrics"] and isinstance(evaluated["metrics"]["objective"], (float, int))
    if task != "survival":
        explanation = api.request(f"/Models/{model_id}/explanation", "POST", {"nsamples": 20}, expected=202)
        assert isinstance(explanation["id"], str)
        results = wait_for(lambda: api.request(f"/Models/{model_id}/explanation/results"), lambda value: value["status"] in {"FINISHED", "CACHED", "CRASHED"}, task + " explanation", timeout)
        assert results["status"] in {"FINISHED", "CACHED"}, results
        assert results["results"] and results.get("report") is not None
        if task == "classification":
            assert results["results"]["importance"]["kind"] == "shap"
            assert results["results"]["performance"]["kind"] == "plot"
            plots = api.request(f"/Models/{model_id}/explanation/plots?key=importance&plots=bar&ps=0")
            assert plots
        else:
            assert results["results"]["residuals"]["kind"] == "plot"
    print(f"PASS {task}: asynchronous baseline/run, persistence, metrics, reports, predictions, evaluation" + (", explanations" if task != "survival" else ", textual event mapping"), flush=True)
    return {"task": task, "project_id": project_id, "process_id": process_id, "model_id": model_id, "remote_job_id": started["id"]}


def check_restart(api, example, timeout):
    process_id, project_id = example["process_id"], example["project_id"]
    restarted = api.request(f"/Processes/{process_id}/restart", "POST", {"maxDuration": 61}, expected=202)
    saved = api.request(f"/Processes/{restarted['processId']}?projectId={project_id}")
    assert saved["options"]["max_duration"] == restarted["maxDuration"] == 61
    stage_duration = saved["options"].get("max_stage_duration")
    assert stage_duration is None or stage_duration <= 61
    fork = copy.deepcopy(saved)
    fork["fit_options"]["patience"] = 100
    launched = api.request("/Processes", "POST", {"projectId": project_id, "pipeline": fork, "maxDuration": 60}, expected=202)
    canceled = api.request(f"/Processes/{launched['processId']}/cancel", "POST", expected=202)
    assert canceled["status"] == "CANCELED"
    final = wait_for(lambda: api.request(f"/Processes/{launched['processId']}/status"), lambda value: value["status"]["current"] in {"CANCELED", "FINISHED", "CACHED"}, "canceled job", timeout)
    assert final["status"]["current"] == "CANCELED"
    print("PASS restart budget persistence, study fork and cancellation", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", default="http://localhost:3000/api")
    parser.add_argument("--iaml-base-url", default="http://localhost:8000")
    parser.add_argument("--timeout", type=int, default=360)
    parser.add_argument("--keep-projects", action="store_true")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--resume", type=Path, help="Reuse the synthetic examples from an earlier run")
    parser.add_argument("--skip-configuration", action="store_true", help="Skip configuration checks already verified in this session")
    parser.add_argument("--task", choices=["all", "classification", "regression", "survival"], default="all")
    args = parser.parse_args()
    api = Api(args.base_url)
    login = api.request("/Auth/login", "POST", {"email": os.environ.get("DRIA_EMAIL", "admin@dria.local"), "password": os.environ.get("DRIA_PASSWORD", "DrIA-Dev123!")})
    api.token = login["accessToken"]
    projects = []
    examples = []
    try:
        if not args.skip_configuration:
            check_configuration(api, Api(args.iaml_base_url))
        previous = json.loads(args.resume.read_text()) if args.resume else []
        for task in (["classification", "regression", "survival"] if args.task == "all" else [args.task]):
            existing = next((item for item in previous if item["task"] == task), None)
            examples.append(check_project(api, task, projects, args.timeout, existing))
            if args.output:
                args.output.write_text(json.dumps(examples, indent=2) + "\n")
        check_restart(api, examples[0], args.timeout)
        if args.output:
            args.output.write_text(json.dumps(examples, indent=2) + "\n")
        print("All integration checks passed.", flush=True)
    finally:
        if args.keep_projects:
            print("Kept synthetic project IDs:", projects, flush=True)
        else:
            for project_id in projects:
                api.request(f"/Projects/{project_id}", "DELETE")


if __name__ == "__main__":
    main()
