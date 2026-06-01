export default function (view) {
    'use strict';

    var getTabs;
    var Shared = null;
    var _sharedPromise = import('/web/configurationpage?name=usermanagement_shared.js').then(function (mod) {
        getTabs = mod.getTabs;
        Shared = mod.createShared(view);
    });

    var _invites = [];
    var _groups = [];
    var _base = '';

    function inviteUrl(token) {
        var base = (_base || ApiClient.serverAddress() || '').replace(/\/+$/, '');
        return base + '/UserManagement/Invite/' + token;
    }

    function groupName(id) {
        for (var i = 0; i < _groups.length; i++) {
            if (_groups[i].Id === id) return _groups[i].Name;
        }
        return '';
    }

    function statusOf(inv) {
        if (!inv.Enabled) return { cls: 'Locked', text: 'Locked' };
        if (inv.ExpiresAt && new Date(inv.ExpiresAt) <= new Date()) return { cls: 'Expired', text: 'Expired' };
        if (inv.MaxUses > 0 && inv.UsedCount >= inv.MaxUses) return { cls: 'Disabled', text: 'Used up' };
        return { cls: 'Active', text: 'Active' };
    }

    function loadAll() {
        _sharedPromise.then(function () {
            Promise.all([
                Shared.apiRequest('Invites', 'GET'),
                Shared.getConfig()
            ]).then(function (results) {
                _invites = results[0] || [];
                _groups = (results[1] && results[1].Groups) || [];
                _base = (results[1] && results[1].InviteBaseUrl) || '';
                populateGroups();
                syncGroupRow();
                renderInvites();
            }).catch(function () {
                _invites = [];
                renderInvites();
            });
        });
    }

    function populateGroups() {
        var sel = Shared.getEl('selGroup');
        if (!sel) return;
        sel.innerHTML = '';
        _groups.forEach(function (g) {
            var opt = document.createElement('option');
            opt.value = g.Id;
            opt.textContent = g.Name || 'Unnamed group';
            sel.appendChild(opt);
        });
    }

    function syncGroupRow() {
        var useDefault = (Shared.getEl('chkUseDefaultGroup') || {}).checked;
        var row = Shared.getEl('groupSelectRow');
        if (row) row.style.display = useDefault ? 'none' : '';
    }

    function renderInvites() {
        var body = view.querySelector('#inviteList');
        var footer = view.querySelector('#inviteFooter');
        if (!body) return;

        if (_invites.length === 0) {
            body.innerHTML = '<div class="pt-empty">No invites yet. Create one above.</div>';
            if (footer) footer.textContent = '';
            return;
        }

        var esc = Shared.escapeHtml;
        var html = '';
        _invites.forEach(function (inv) {
            var st = statusOf(inv);
            var url = inviteUrl(inv.Token);
            var uses = (inv.UsedCount || 0) + (inv.MaxUses > 0 ? ' / ' + inv.MaxUses : ' / ∞');
            var meta = [];
            meta.push('Uses: ' + uses);
            if (inv.UseDefaultGroup) meta.push('Group: Default');
            else if (inv.GroupId) meta.push('Group: ' + esc(groupName(inv.GroupId)));
            if (inv.PinHash) meta.push('PIN set');
            if (inv.ExpiresAt) meta.push('Expires ' + esc(Shared.formatDate(inv.ExpiresAt)));

            html += '<div class="pt-row" data-id="' + esc(inv.Id) + '" style="flex-direction:column; align-items:stretch; gap:8px;">'
                + '<div style="display:flex; align-items:center; gap:8px;">'
                + '<div class="um-item-info" style="flex:1;">'
                + '<div class="um-item-title">' + (esc(inv.Label) || 'Untitled invite') + '</div>'
                + '<div class="um-item-sub">' + meta.join(' • ') + '</div>'
                + '</div>'
                + Shared.badge(st.cls, st.text)
                + '<button type="button" class="um-btn um-del" title="Delete"><span class="material-icons">delete</span></button>'
                + '</div>'
                + '<div style="display:flex; gap:8px; align-items:center;">'
                + '<input class="um-edit-input um-url" readonly value="' + esc(url) + '" style="border:1px solid var(--um-border);" />'
                + '<button is="emby-button" type="button" class="raised button-small um-copy"><span>Copy</span></button>'
                + '</div>'
                + '</div>';
        });

        body.innerHTML = html;
        if (footer) footer.textContent = _invites.length + ' invite' + (_invites.length !== 1 ? 's' : '');

        body.querySelectorAll('.pt-row').forEach(function (row) {
            var id = row.getAttribute('data-id');
            var del = row.querySelector('.um-del');
            var copy = row.querySelector('.um-copy');
            var urlInput = row.querySelector('.um-url');
            if (del) del.addEventListener('click', function () { deleteInvite(id); });
            if (copy) copy.addEventListener('click', function () { copyUrl(urlInput); });
        });
    }

    function copyUrl(input) {
        if (!input) return;
        input.select();
        var done = function () { Shared.setStatus('inviteStatus', 'Link copied.', false); };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(input.value).then(done).catch(function () {
                document.execCommand('copy'); done();
            });
        } else {
            document.execCommand('copy'); done();
        }
    }

    function createInvite() {
        var expVal = (Shared.getEl('dateExpiry') || {}).value || '';
        var maxUses = parseInt(Shared.getEl('txtMaxUses').value, 10);
        var useDefault = Shared.getEl('chkUseDefaultGroup').checked;
        var groupId = Shared.getEl('selGroup').value || null;

        if (!useDefault && !groupId) {
            Shared.setStatus('inviteStatus', 'Choose a group for this invite.', true);
            return;
        }

        var payload = {
            Label: Shared.getEl('txtLabel').value || '',
            Pin: Shared.getEl('txtPin').value || '',
            UseDefaultGroup: useDefault,
            GroupId: useDefault ? null : groupId,
            ExpiresAt: expVal ? new Date(expVal).toISOString() : null,
            MaxUses: isNaN(maxUses) || maxUses < 0 ? 0 : maxUses
        };

        Shared.apiRequest('Invites', 'POST', payload)
            .then(function () {
                Shared.setStatus('inviteStatus', 'Invite created.', false);
                Shared.getEl('txtLabel').value = '';
                Shared.getEl('txtPin').value = '';
                Shared.getEl('dateExpiry').value = '';
                loadAll();
            })
            .catch(function () {
                Shared.setStatus('inviteStatus', 'Failed to create invite.', true);
            });
    }

    function deleteInvite(id) {
        if (!confirm('Delete this invite? The link will stop working.')) return;
        Shared.apiRequest('Invites/' + id, 'DELETE')
            .then(function () {
                Shared.setStatus('inviteStatus', 'Invite deleted.', false);
                loadAll();
            })
            .catch(function () {
                Shared.setStatus('inviteStatus', 'Failed to delete invite.', true);
            });
    }

    view.addEventListener('viewshow', function () {
        _sharedPromise.then(function () {
            LibraryMenu.setTabs('usermanagement', 1, getTabs);
            loadAll();
        });
    });

    _sharedPromise.then(function () {
        Shared.initCollapsibles();
        var btn = Shared.getEl('btnCreateInvite');
        if (btn) btn.addEventListener('click', createInvite);
        var chk = Shared.getEl('chkUseDefaultGroup');
        if (chk) chk.addEventListener('change', syncGroupRow);
    });
}
