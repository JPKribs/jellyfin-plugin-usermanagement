"""Takes the Activity Log screenshot: the plugin's entries on Jellyfin's own Activity page, which only a real server draws.

Usage: python3 -I activity.py   (run ./build.sh first, so dist/ holds the plugin ZIP)
It starts a throwaway Jellyfin in Docker with the plugin from the newest ZIP in dist/, makes a generic admin with a
random password, creates two invites, redeems one, enters a wrong PIN on the other, then has shoot.mjs open the
Activity page. The container and its folder are removed afterwards. IMAGE, PORT (default 8844) and the shoot.mjs
variables (PLAYWRIGHT_CORE, CHROMIUM) can be set in the environment.
"""
import glob, json, os, re, secrets, shutil, subprocess, sys, tempfile, time, urllib.error, urllib.request, zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
IMAGE = os.environ.get('IMAGE', 'jellyfin/jellyfin:12.4.20261026-211555')
PORT = int(os.environ.get('PORT', '8844'))
NAME = 'um-activity-shot'
PLUGIN_ID = re.search(r'^guid: *"?([0-9a-f-]+)', open(os.path.join(REPO, 'build.yaml')).read(), re.M).group(1)
ROOT = 'http://127.0.0.1:%d' % PORT
AUTH = 'MediaBrowser Client="screenshots", Device="screenshots", DeviceId="um-activity-shot", Version="1.0"'


def call(method, path, body=None, token=None):
    headers = {'Content-Type': 'application/json', 'Authorization': AUTH + (', Token="%s"' % token if token else '')}
    data = json.dumps(body).encode() if body is not None else None
    try:
        with urllib.request.urlopen(urllib.request.Request(ROOT + path, data=data, method=method, headers=headers), timeout=30) as r:
            text = r.read().decode()
            return json.loads(text) if text.strip() else None
    except urllib.error.HTTPError as ex:
        ex.msg = '%s %s: %s' % (method, path, ex.read().decode()[:300])
        raise


def wait(what, check, timeout=180):
    end = time.time() + timeout
    while time.time() < end:
        try:
            if check():
                return
        except (urllib.error.URLError, ConnectionError, OSError):
            pass
        time.sleep(2)
    sys.exit('Timed out waiting for ' + what + '.')


zips = sorted(glob.glob(os.path.join(REPO, 'dist', '*.zip')), key=os.path.getmtime)
if not zips:
    sys.exit('No plugin ZIP in dist/. Run ./build.sh first.')
work = tempfile.mkdtemp(prefix='um-activity-')
plugin_dir = os.path.join(work, 'config', 'plugins', 'UserManagement')
os.makedirs(plugin_dir)
os.makedirs(os.path.join(work, 'cache'))
with zipfile.ZipFile(zips[-1]) as z:
    z.extractall(plugin_dir)

subprocess.run(['docker', 'rm', '-f', NAME], capture_output=True)
user = ['--user', '%d:%d' % (os.getuid(), os.getgid())]
subprocess.run(['docker', 'run', '-d', '--name', NAME, *user, '-p', '%d:8096' % PORT, '-v', work + '/config:/config',
                '-v', work + '/cache:/cache', IMAGE], check=True, capture_output=True)
try:
    wait('the server', lambda: call('GET', '/System/Info/Public').get('Version'))
    wait('the startup wizard', lambda: call('GET', '/Startup/Configuration') is not None)
    password = secrets.token_urlsafe(16)
    call('POST', '/Startup/Configuration', {'UICulture': 'en-US', 'MetadataCountryCode': 'US', 'PreferredMetadataLanguage': 'en'})
    call('GET', '/Startup/User')
    call('POST', '/Startup/User', {'Name': 'admin', 'Password': password})
    call('POST', '/Startup/Complete')
    token = call('POST', '/Users/AuthenticateByName', {'Username': 'admin', 'Pw': password})['AccessToken']

    # One group, the default, for new accounts to join.
    plugin = '/Plugins/%s/Configuration' % PLUGIN_ID
    config = call('GET', plugin, token=token)
    group = 'b4e7f2a0-5c1d-4e8a-9f36-2d7a1c0e9b58'
    config.update(Groups=[{'Id': group, 'Name': 'Family', 'MemberIds': []}], DefaultGroupId=group)
    call('POST', plugin, config, token)

    family = call('POST', '/UserManagement/Invites', {'Label': 'Family and Friends', 'MaxUses': 1}, token)
    club = call('POST', '/UserManagement/Invites', {'Label': 'Book Club', 'Pin': '482615'}, token)
    call('POST', '/Invite/%s/Redeem' % family['Token'], {'Username': 'jordan', 'Password': secrets.token_urlsafe(12)})
    try:
        call('POST', '/Invite/%s/Redeem' % club['Token'], {'Pin': '000000', 'Username': 'guest', 'Password': secrets.token_urlsafe(12)})
    except urllib.error.HTTPError:
        pass
    # Wrong PINs are summed into one entry, written after 30 quiet seconds.
    wait('the PIN summary', lambda: any(e['Type'] == 'UserManagement.InvitePinFailed'
                                        for e in call('GET', '/System/ActivityLog/Entries?limit=50', token=token)['Items']), 120)

    env = dict(os.environ, JELLYFIN_URL=ROOT, JELLYFIN_TOKEN=token,
               JELLYFIN_USER=call('GET', '/Users/Me', token=token)['Id'], JELLYFIN_SERVER=call('GET', '/System/Info/Public')['Id'])
    subprocess.run(['node', os.path.join(HERE, 'shoot.mjs'), 'Activity Log'], env=env, check=True)
finally:
    subprocess.run(['docker', 'rm', '-f', NAME], capture_output=True)
    shutil.rmtree(work, ignore_errors=True)
