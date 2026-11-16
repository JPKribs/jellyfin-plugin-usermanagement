"""User Management screenshot server: serves the plugin pages, Base and jellyfin-web the way Jellyfin does, and the
invite signup page at /invite/<token> through Base's status template the way InviteController does.

Usage: python3 -I server.py [port]   (default 8842)
Base comes from the NuGet cache at the version the plugin's csproj references. JELLYFIN_WEB picks the jellyfin-web folder.
"""
import http.server, json, os, re, sys, urllib.parse
HERE = os.path.dirname(os.path.abspath(__file__))
PLUGIN = os.path.join(HERE, '..', '..', 'Jellyfin.Plugin.UserManagement')
CONF = os.path.join(PLUGIN, 'Configuration')
JF = os.environ.get('JELLYFIN_WEB', '/Applications/Jellyfin.app/Contents/Resources/jellyfin-web')
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8842


def base_assets():
    """Base's assets folder in the NuGet cache, at the version the csproj names."""
    csproj = open(os.path.join(PLUGIN, 'Jellyfin.Plugin.UserManagement.csproj')).read()
    m = re.search(r'Include="JPKribs\.Jellyfin\.Base"\s+Version="([^"]+)"', csproj)
    if not m:
        sys.exit('No JPKribs.Jellyfin.Base PackageReference in the csproj.')
    path = os.path.expanduser('~/.nuget/packages/jpkribs.jellyfin.base/%s/assets' % m.group(1))
    if not os.path.isdir(path):
        sys.exit('Base %s is not in the NuGet cache (%s). Run dotnet restore on the plugin first.' % (m.group(1), path))
    return path


def status_page(content):
    """Mirrors InviteController: StatusPage.Render("", "", title: "Invite", buttonText: null, content: the signup page)."""
    values = {'TITLE': 'Invite', 'HEADING': '', 'MESSAGE': '', 'SPINNER': '', 'BUTTON': '', 'CONTENT': content}
    template = open(os.path.join(BASE, '..', 'templates', 'status.html'), encoding='utf-8').read()
    return re.sub(r'\{\{([A-Z_]+)\}\}', lambda m: values.get(m.group(1), m.group(0)), template)


# What the signup page asks the server for, with a generic invite that needs a PIN.
SIGNUP = {
    'Info': {'Valid': True, 'RequiresPin': True, 'Message': 'Welcome to the family server. Pick a username and password to get started.'},
    'System/Info/Public': {'ServerName': 'Jellyfin'},
}


def stylesheets():
    """jellyfin-web's stylesheets in load order: index.html's two first, every other chunk by name, then the dark theme."""
    first = re.findall(r'href="([^"?]+\.css)', open(os.path.join(JF, 'index.html')).read())
    rest = [n for n in sorted(os.listdir(JF)) if n.endswith('.css') and n not in first]
    return ''.join('<link rel="stylesheet" href="/jf/%s">\n' % n for n in first + rest + ['themes/dark/theme.css'])


if not os.path.isfile(os.path.join(JF, 'index.html')):
    sys.exit('No jellyfin-web at %s. Set JELLYFIN_WEB to a jellyfin-web folder.' % JF)
BASE = base_assets()
LINKS = stylesheets()
TYPES = {'.woff2': 'font/woff2', '.woff': 'font/woff', '.ttf': 'font/ttf', '.css': 'text/css', '.js': 'text/javascript',
         '.mjs': 'text/javascript', '.svg': 'image/svg+xml', '.png': 'image/png'}


class H(http.server.BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def do_GET(self):
        u = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(u.query)
        invite = re.match(r'^/invite/[^/]+(/Info)?$', u.path)
        if invite:
            if invite.group(1):
                return self.send(json.dumps(SIGNUP['Info']).encode(), 'application/json')
            page = status_page(open(os.path.join(CONF, 'usermanagement_invite_public.html'), encoding='utf-8').read())
            return self.send(page.encode(), 'text/html; charset=utf-8')
        if u.path == '/System/Info/Public':
            return self.send(json.dumps(SIGNUP['System/Info/Public']).encode(), 'application/json')
        if u.path.startswith('/web/'):
            u = u._replace(path='/jf/' + u.path[5:])
        if u.path.endswith('configurationpage') and 'name' in q:
            name = q['name'][0]
            if name.startswith('usermanagement_jpkribs_shared'):
                path = os.path.join(BASE, 'jpkribs_shared' + os.path.splitext(name)[1])
            else:
                path = os.path.join(CONF, name if '.' in name else name + '.html')
        elif u.path.startswith('/jf/'):
            path = os.path.join(JF, urllib.parse.unquote(u.path[4:]))
        else:
            path = os.path.join(HERE, u.path.lstrip('/'))
        if not os.path.isfile(path):
            self.send_error(404)
            return
        data = open(path, 'rb').read()
        if os.path.dirname(os.path.abspath(path)) == HERE and path.endswith('.html'):
            data = data.replace(b'<!--stylesheets-->\n', LINKS.encode())
        self.send(data, TYPES.get(os.path.splitext(path)[1], 'text/html'))

    def send(self, data, kind):
        self.send_response(200)
        self.send_header('Content-Type', kind)
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(data)


http.server.ThreadingHTTPServer(('127.0.0.1', PORT), H).serve_forever()
