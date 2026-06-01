export default function (view) {
    'use strict';

    var getTabs;
    var Shared = null;
    var _sharedPromise = import('/web/configurationpage?name=usermanagement_shared.js').then(function (mod) {
        getTabs = mod.getTabs;
        Shared = mod.createShared(view);
    });

    var PROVIDER_ID = 'Jellyfin.Plugin.UserManagement.Passwords.PasswordRuleAuthenticationProvider';
    var _users = [];
    var _enroll = {}; // userId -> pending enrolled bool (survives search re-renders)

    // ── Loading ──────────────────────────────────────────────

    function loadAll() {
        _sharedPromise.then(function () {
            Promise.all([Shared.getConfig(), ApiClient.getUsers()]).then(function (results) {
                var config = results[0];
                _users = results[1] || [];

                Shared.getEl('txtInviteBaseUrl').value = config.InviteBaseUrl || '';
                Shared.getEl('txtPwLength').value = config.PasswordMinLength || 8;
                Shared.getEl('chkPwNoEmpty').checked = config.PasswordDisallowEmpty === true;
                Shared.getEl('chkPwUpper').checked = config.PasswordRequireUppercase === true;
                Shared.getEl('chkPwNumber').checked = config.PasswordRequireNumber === true;
                Shared.getEl('chkPwSymbol').checked = config.PasswordRequireSymbol === true;
                Shared.getEl('chkPwApplyNew').checked = config.PasswordRulesApplyToNewUsers === true;
                populateGroups(config);

                _enroll = {};
                _users.forEach(function (u) {
                    _enroll[u.Id] = !!(u.Policy && u.Policy.AuthenticationProviderId === PROVIDER_ID);
                });
                renderPwUsers();
            });
        });
    }

    function populateGroups(config) {
        var sel = Shared.getEl('selDefaultGroup');
        if (!sel) return;
        while (sel.options.length > 1) sel.remove(1);
        (config.Groups || []).forEach(function (g) {
            var opt = document.createElement('option');
            opt.value = g.Id;
            opt.textContent = g.Name || 'Unnamed group';
            sel.appendChild(opt);
        });
        sel.value = config.DefaultGroupId || '';
    }

    // ── Enrollment checklist ─────────────────────────────────

    function isAdmin(u) {
        return !!(u.Policy && u.Policy.IsAdministrator);
    }

    function renderPwUsers() {
        var esc = Shared.escapeHtml;
        var term = ((Shared.getEl('pwUserSearch') || {}).value || '').toLowerCase();
        var list = Shared.getEl('pwUserList');

        var rows = '';
        _users.forEach(function (u) {
            if (term && (u.Name || '').toLowerCase().indexOf(term) === -1) return;
            var admin = isAdmin(u);
            rows += '<label class="um-check-row' + (admin ? ' disabled' : '') + '">'
                + '<input type="checkbox" class="pw-user-toggle" data-id="' + esc(u.Id) + '"'
                + (_enroll[u.Id] ? ' checked' : '') + (admin ? ' disabled' : '') + ' />'
                + '<span class="um-check-name">' + esc(u.Name) + '</span>'
                + (admin ? '<span class="um-check-note admin">Admin</span>' : '')
                + '</label>';
        });

        list.innerHTML = rows || '<div class="um-item-sub" style="padding:8px;">No users match.</div>';
        list.querySelectorAll('.pw-user-toggle').forEach(function (cb) {
            cb.addEventListener('change', function () {
                _enroll[cb.getAttribute('data-id')] = cb.checked;
                updatePwSummary();
            });
        });
        updatePwSummary();
    }

    function updatePwSummary() {
        var n = Object.keys(_enroll).filter(function (id) { return _enroll[id]; }).length;
        var el = Shared.getEl('pwUserSummary');
        if (el) el.textContent = n + ' user' + (n !== 1 ? 's' : '') + ' enrolled';
    }

    // ── Save ─────────────────────────────────────────────────

    function save() {
        Shared.getConfig().then(function (config) {
            config.DefaultGroupId = Shared.getEl('selDefaultGroup').value || null;
            config.InviteBaseUrl = (Shared.getEl('txtInviteBaseUrl').value || '').trim().replace(/\/+$/, '');
            var pwLen = parseInt(Shared.getEl('txtPwLength').value, 10);
            config.PasswordMinLength = isNaN(pwLen) || pwLen < 1 ? 8 : pwLen;
            config.PasswordDisallowEmpty = Shared.getEl('chkPwNoEmpty').checked;
            config.PasswordRequireUppercase = Shared.getEl('chkPwUpper').checked;
            config.PasswordRequireNumber = Shared.getEl('chkPwNumber').checked;
            config.PasswordRequireSymbol = Shared.getEl('chkPwSymbol').checked;
            config.PasswordRulesApplyToNewUsers = Shared.getEl('chkPwApplyNew').checked;

            var enrolledIds = Object.keys(_enroll).filter(function (id) { return _enroll[id]; });

            Shared.saveConfig(config).then(function () {
                return Shared.apiRequest('Passwords/Enrollment', 'POST', { UserIds: enrolledIds });
            }).then(function () {
                Shared.setStatus('settingsStatus', 'Settings saved.', false);
                loadAll();
            }).catch(function () {
                Shared.setStatus('settingsStatus', 'Failed to save settings.', true);
            });
        });
    }

    function revertAll() {
        if (!confirm('Remove all users from password-rule enforcement and return them to the built-in provider?')) return;
        Shared.apiRequest('Passwords/Unassign', 'POST')
            .then(function (r) {
                Shared.setStatus('settingsStatus', 'Reverted ' + ((r && r.Reverted) || 0) + ' user(s).', false);
                loadAll();
            })
            .catch(function () {
                Shared.setStatus('settingsStatus', 'Failed to revert.', true);
            });
    }

    // ── Listeners ────────────────────────────────────────────

    view.addEventListener('viewshow', function () {
        _sharedPromise.then(function () {
            LibraryMenu.setTabs('usermanagement', 2, getTabs);
            loadAll();
        });
    });

    _sharedPromise.then(function () {
        Shared.initCollapsibles();
        var btn = Shared.getEl('btnSaveSettings');
        if (btn) btn.addEventListener('click', save);
        var revert = Shared.getEl('btnPwRevertAll');
        if (revert) revert.addEventListener('click', revertAll);
        var search = Shared.getEl('pwUserSearch');
        if (search) search.addEventListener('input', renderPwUsers);
    });
}
