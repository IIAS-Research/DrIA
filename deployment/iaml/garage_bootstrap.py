"""Initialize the single-node object store and its private application credentials."""

import argparse
import json
import os
from pathlib import Path
import re
import secrets
import sys
import tempfile
import time
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


SECRET_DIRECTORY = Path('/run/garage-secrets')
CREDENTIALS_FILE = Path('/run/iaml-storage/s3.json')
KEY_NAME = 'dria-iaml'


class BootstrapError(RuntimeError):
    pass


class AdminUnavailable(BootstrapError):
    pass


def write_private(path, value):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8',
                                         dir=path.parent, delete=False) as output:
            temporary = Path(output.name)
            os.fchmod(output.fileno(), 0o600)
            output.write(value)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, path)
        temporary = None
        directory_fd = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    finally:
        if temporary is not None:
            temporary.unlink()


def prepare_secrets(directory=SECRET_DIRECTORY):
    for name in ('rpc-secret', 'admin-token'):
        path = directory / name
        if path.exists():
            if not re.fullmatch(r'[0-9a-f]{64}', path.read_text().strip()):
                raise BootstrapError(f'Invalid existing Garage {name} file.')
            if path.stat().st_mode & 0o077:
                raise BootstrapError(f'Garage {name} file must have private permissions.')
        else:
            write_private(path, secrets.token_hex(32) + '\n')


class AdminClient:
    def __init__(self, endpoint, token):
        self.endpoint = endpoint.rstrip('/')
        self.token = token

    def call(self, operation, *, body=None, params=None):
        endpoint = f'{self.endpoint}/v2/{operation}'
        if params:
            endpoint += '?' + urlencode(params)
        request = Request(endpoint, method='GET' if body is None else 'POST',
                          data=None if body is None else json.dumps(body).encode(),
                          headers={'Authorization': f'Bearer {self.token}',
                                   'Content-Type': 'application/json'})
        try:
            with urlopen(request, timeout=5) as response:
                content = response.read(1024 * 1024 + 1)
        except HTTPError as error:
            exception = AdminUnavailable if 500 <= error.code <= 599 else BootstrapError
            raise exception(f'Garage {operation} returned HTTP {error.code}.') from error
        except (URLError, TimeoutError, OSError) as error:
            raise AdminUnavailable(f'Garage {operation} could not connect.') from error
        if len(content) > 1024 * 1024:
            raise BootstrapError(f'Garage {operation} response is too large.')
        try:
            return json.loads(content)
        except (ValueError, RecursionError) as error:
            raise BootstrapError(f'Garage {operation} response is not valid JSON.') from error


def wait_ready(admin):
    deadline = time.monotonic() + 120
    while True:
        try:
            health = admin.call('GetClusterHealth')
            if not isinstance(health, dict) or health.get('status') not in (
                    'healthy', 'degraded', 'unavailable'):
                raise BootstrapError('Garage returned an invalid cluster health response.')
            if health['status'] == 'healthy':
                return
        except AdminUnavailable:
            pass
        if time.monotonic() >= deadline:
            raise BootstrapError('Garage did not become healthy before the startup deadline.')
        time.sleep(0.5)


def load_credentials(path):
    if not path.exists():
        return None
    try:
        credentials = json.loads(path.read_text(encoding='utf-8'))
    except (ValueError, RecursionError) as error:
        raise BootstrapError('Existing application credentials are not valid JSON.') from error
    if not isinstance(credentials, dict) or set(credentials) != {'access_key', 'secret_key'} \
            or any(not isinstance(value, str) or not value or value.strip() != value
                   for value in credentials.values()):
        raise BootstrapError('Existing application credentials have an invalid format.')
    if path.stat().st_mode & 0o077:
        raise BootstrapError('Application credentials must have private permissions.')
    return credentials


def provision(admin, bucket, path):
    buckets = admin.call('ListBuckets')
    matches = [item for item in buckets if bucket in item.get('globalAliases', [])]
    if len(matches) > 1:
        raise BootstrapError('The application bucket alias is ambiguous.')
    bucket_info = matches[0] if matches else admin.call('CreateBucket', body={'globalAlias': bucket})
    bucket_id = bucket_info['id']

    credentials = load_credentials(path)
    if credentials is not None:
        key = admin.call('GetKeyInfo', params={'id': credentials['access_key'], 'showSecretKey': 'true'})
    else:
        matches = [item for item in admin.call('ListKeys') if item.get('name') == KEY_NAME]
        if len(matches) > 1:
            raise BootstrapError('The application access key name is ambiguous.')
        key = (admin.call('GetKeyInfo', params={'id': matches[0]['id'], 'showSecretKey': 'true'})
               if matches else admin.call('CreateKey', body={
                   'name': KEY_NAME, 'neverExpires': True, 'deny': {'createBucket': True}}))
    if key.get('name') != KEY_NAME or key.get('expired') is not False \
            or not key.get('accessKeyId') or not key.get('secretAccessKey'):
        raise BootstrapError('Garage did not return a valid application access key.')
    returned = {'access_key': key['accessKeyId'], 'secret_key': key['secretAccessKey']}
    if credentials is not None and returned != credentials:
        raise BootstrapError('Persisted credentials do not match the Garage access key.')
    if credentials is None:
        write_private(path, json.dumps(returned) + '\n')
    admin.call('UpdateKey', params={'id': returned['access_key']},
               body={'deny': {'createBucket': True}, 'neverExpires': True})
    permission = {'bucketId': bucket_id, 'accessKeyId': returned['access_key']}
    admin.call('AllowBucketKey', body={**permission, 'permissions': {'read': True, 'write': True}})
    admin.call('DenyBucketKey', body={**permission, 'permissions': {'owner': True}})


def check_s3(endpoint, region, bucket, path):
    from minio import Minio
    import urllib3
    credentials = load_credentials(path)
    client = Minio(endpoint, access_key=credentials['access_key'], secret_key=credentials['secret_key'],
                   region=region, secure=False, http_client=urllib3.PoolManager(
                       timeout=urllib3.Timeout(connect=3, read=5), retries=False))
    try:
        if not client.bucket_exists(bucket):
            raise BootstrapError('The application bucket is missing.')
    except Exception as error:
        raise BootstrapError('Application credentials cannot access the S3 bucket.') from error


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=('secrets', 'init'))
    arguments = parser.parse_args()
    try:
        if arguments.operation == 'secrets':
            prepare_secrets()
            print('Garage private configuration is ready.')
            return
        token = (SECRET_DIRECTORY / 'admin-token').read_text().strip()
        admin = AdminClient(os.environ.get('GARAGE_ADMIN_ENDPOINT', 'http://garage:3903'), token)
        bucket = os.environ.get('S3_BUCKET', 'dria-iaml')
        path = Path(os.environ.get('S3_CREDENTIALS_FILE', str(CREDENTIALS_FILE)))
        wait_ready(admin)
        provision(admin, bucket, path)
        check_s3(os.environ.get('S3_ENDPOINT', 'garage:3900'),
                 os.environ.get('S3_REGION', 'garage'), bucket, path)
        print('Garage application storage is ready.')
    except (BootstrapError, OSError, KeyError, TypeError) as error:
        print(f'Garage initialization failed: {error}', file=sys.stderr)
        raise SystemExit(1) from None


if __name__ == '__main__':
    main()
