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
    var _defaultGroupId = '';

    function inviteUrl(token) {
        var base = (_base || ApiClient.serverAddress() || '').replace(/\/+$/, '');
        return base + '/invite/' + token;
    }

    function groupName(id) {
        for (var i = 0; i < _groups.length; i++) {
            if (_groups[i].Id === id) return _groups[i].Name;
        }
        return '';
    }

    function statusOf(inv) {
        if (inv.ExpiresAt && new Date(inv.ExpiresAt) <= new Date()) return { cls: 'Expired', text: 'Expired' };
        if (!inv.Enabled) return { cls: 'Locked', text: 'Locked' };
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
                _defaultGroupId = (results[1] && results[1].DefaultGroupId) || '';
                var urlEl = Shared.getEl('txtInviteBaseUrl');
                if (urlEl) urlEl.value = _base;
                var rcEl = Shared.getEl('txtRateCount');
                if (rcEl) rcEl.value = (results[1] && results[1].InviteRateLimitCount) || 0;
                var rwEl = Shared.getEl('txtRateWindow');
                if (rwEl) rwEl.value = (results[1] && results[1].InviteRateLimitWindowMinutes) || 0;
                updateHttpsWarning();
                var pinHelp = Shared.getEl('pinHelp');
                if (pinHelp) {
                    var attempts = (results[1] && results[1].MaxPinAttempts) || 5;
                    pinHelp.textContent = 'A short code the invitee must enter. The invite locks itself after ' + attempts + ' wrong tries.';
                }
                populateGroups();
                renderInvites();
            }).catch(function () {
                _invites = [];
                renderInvites();
            });
        });
    }

    var _newResources = [];

    function renderResourceList() {
        var box = Shared.getEl('resList');
        if (!box) return;
        Shared.setVisible('resListWrap', _newResources.length > 0);
        var esc = Shared.escapeHtml;
        box.innerHTML = _newResources.map(function (res, i) {
            return '<div class="jpk-table-row" style="align-items:center; gap:8px;">'
                + '<div class="jpk-table-item-info" style="flex:1;">'
                + '<div class="jpk-table-item-title" style="font-weight:bold;">' + esc(res.Title) + '</div>'
                + '<div class="jpk-table-item-sub">' + esc(res.Url) + '</div>'
                + '</div>'
                + '<button type="button" class="um-btn um-res-del" data-index="' + i + '" title="Remove"><span class="material-icons">delete</span></button>'
                + '</div>';
        }).join('');
    }

    function addResource() {
        var title = (Shared.getEl('txtResTitle').value || '').trim();
        var url = (Shared.getEl('txtResUrl').value || '').trim();
        if (!title || !/^https?:\/\/.+/i.test(url)) {
            Shared.setStatus('inviteStatus', 'A resource needs a title and a full http(s) URL.', true);
            return;
        }
        _newResources.push({ Title: title, Url: url });
        Shared.getEl('txtResTitle').value = '';
        Shared.getEl('txtResUrl').value = '';
        renderResourceList();
    }

    // Groups that disallow all password changes are admin managed and cannot take invites, so they
    // are left out of the dropdown. The server rejects them too.
    function blocksInvites(g) {
        var pw = g.Password || {};
        var mode = typeof pw.ChangeMode === 'number' ? pw.ChangeMode === 2 : pw.ChangeMode === 'Disallowed';
        return pw.Enabled === true && mode;
    }

    function populateGroups() {
        var sel = Shared.getEl('selGroup');
        if (!sel) return;
        sel.innerHTML = '';
        _groups.forEach(function (g) {
            if (blocksInvites(g)) return;
            var opt = document.createElement('option');
            opt.value = g.Id;
            opt.textContent = g.Name || 'Unnamed group';
            sel.appendChild(opt);
        });
        if (_defaultGroupId && _groups.some(function (g) { return g.Id === _defaultGroupId; })) {
            sel.value = _defaultGroupId;
        }
    }

    function card(cls, count, label) {
        return '<div class="jpk-card ' + cls + '"><span class="jpk-card-count">' + count
            + '</span><span class="jpk-card-label">' + label + '</span></div>';
    }

    function renderInviteCards() {
        var container = view.querySelector('#inviteCards');
        if (!container) return;
        var now = new Date();
        var active = 0, expired = 0, locked = 0, consumed = 0;
        _invites.forEach(function (inv) {
            if (inv.ExpiresAt && new Date(inv.ExpiresAt) <= now) { expired++; }
            else if (!inv.Enabled) { locked++; }
            else if (inv.MaxUses > 0 && inv.UsedCount >= inv.MaxUses) { consumed++; }
            else { active++; }
        });
        container.innerHTML =
            card('green', active, 'Active') +
            card('yellow', locked, 'Locked') +
            card('purple', consumed, 'Consumed') +
            card('red', expired, 'Expired');
    }

    function renderInvites() {
        renderInviteCards();
        var body = view.querySelector('#inviteList');
        var footer = view.querySelector('#inviteFooter');
        if (!body) return;

        if (_invites.length === 0) {
            body.innerHTML = Shared.emptySection('No invites yet. Create one above.');
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
            if (inv.HasPin) meta.push('PIN set');

            var toggleIcon = inv.Enabled ? 'lock' : 'lock_open';
            var toggleTitle = inv.Enabled ? 'Disable invite' : 'Enable invite';

            html += '<div class="jpk-table-row" data-id="' + esc(inv.Id) + '" style="flex-direction:column; align-items:stretch; gap:8px;">'
                + '<div style="display:flex; align-items:center; gap:8px;">'
                + '<div class="jpk-table-item-info" style="flex:1;">'
                + '<div class="jpk-table-item-title" style="font-weight: bold;">' + (esc(inv.Label) || 'Untitled invite') + '</div>'
                + '<div class="jpk-table-item-sub">' + meta.join(' • ') + '</div>'
                + '</div>'
                + '<span class="jpk-table-status-badge ' + st.cls + '">' + esc(st.text) + '</span>'
                + '<button type="button" class="um-btn um-dup" title="Duplicate invite"><span class="material-icons">content_copy</span></button>'
                + '<button type="button" class="um-btn um-toggle" title="' + toggleTitle + '"><span class="material-icons">' + toggleIcon + '</span></button>'
                + '<button type="button" class="um-btn um-del" title="Delete"><span class="material-icons">delete</span></button>'
                + '</div>'
                + '<div style="display:flex; gap:8px; align-items:center;">'
                + '<input class="jpk-edit-input um-url" readonly value="' + esc(url) + '" />'
                + '<button is="emby-button" type="button" class="raised jpk-button-small um-copy"><span>Copy</span></button>'
                + '</div>'
                + '<div style="display:flex; gap:8px; align-items:center;">'
                + '<label style="font-size:0.85em; opacity:0.7; min-width:54px;">Expires</label>'
                + '<input type="date" class="jpk-edit-input um-expiry" value="' + (inv.ExpiresAt ? esc(String(inv.ExpiresAt).slice(0, 10)) : '') + '" style="max-width:240px;" />'
                + '</div>'
                + '</div>';
        });

        body.innerHTML = html;
        if (footer) footer.textContent = _invites.length + ' invite' + (_invites.length !== 1 ? 's' : '');

        body.querySelectorAll('.jpk-table-row').forEach(function (row) {
            var id = row.getAttribute('data-id');
            var del = row.querySelector('.um-del');
            var copy = row.querySelector('.um-copy');
            var urlInput = row.querySelector('.um-url');
            var toggle = row.querySelector('.um-toggle');
            if (toggle) toggle.addEventListener('click', function () {
                var inv = _invites.filter(function (x) { return String(x.Id) === String(id); })[0];
                setInviteEnabled(id, !(inv && inv.Enabled));
            });
            var expiryInput = row.querySelector('.um-expiry');
            if (expiryInput) expiryInput.addEventListener('change', function () { setInviteExpiry(id, this.value); });
            if (del) del.addEventListener('click', function () { deleteInvite(id); });
            if (copy) copy.addEventListener('click', function () { copyUrl(urlInput); });
            var dup = row.querySelector('.um-dup');
            if (dup) dup.addEventListener('click', function () { duplicateInvite(id); });
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

    function updateExpiryState() {
        Shared.setVisible('expiryFields', !!(Shared.getEl('chkSetExpiry') || {}).checked);
    }

    function updateMaxUsesState() {
        Shared.setVisible('maxUsesFields', !!(Shared.getEl('chkSetMaxUses') || {}).checked);
    }


    function openCreateSection() {
        var header = view.querySelector('.jpk-collapsible-header[data-target="newInviteContent"]');
        var content = Shared.getEl('newInviteContent');
        if (header && content && content.classList.contains('collapsed')) {
            header.click();
        }
        if (content && content.scrollIntoView) {
            content.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    function duplicateInvite(id) {
        var inv = _invites.filter(function (x) { return String(x.Id) === String(id); })[0];
        if (!inv) return;

        Shared.getEl('txtLabel').value = inv.Label ? inv.Label + ' copy' : '';
        Shared.getEl('txtMessage').value = inv.Message || '';

        _newResources = (inv.Resources || []).map(function (r) {
            return { Title: r.Title, Url: r.Url };
        });
        renderResourceList();

        var sel = Shared.getEl('selGroup');
        var groupId = inv.UseDefaultGroup ? _defaultGroupId : inv.GroupId;
        if (sel && groupId && _groups.some(function (g) { return g.Id === groupId; })) {
            sel.value = groupId;
        }

        var hasExpiry = !!inv.ExpiresAt;
        Shared.getEl('chkSetExpiry').checked = hasExpiry;
        Shared.getEl('dateExpiry').value = hasExpiry ? String(inv.ExpiresAt).slice(0, 10) : '';
        updateExpiryState();

        var hasMaxUses = inv.MaxUses > 0;
        Shared.getEl('chkSetMaxUses').checked = hasMaxUses;
        Shared.getEl('txtMaxUses').value = hasMaxUses ? String(inv.MaxUses) : '1';
        updateMaxUsesState();

        // The PIN is stored only as a salted hash, so it cannot be carried over.
        Shared.getEl('txtPin').value = '';

        openCreateSection();
        Shared.setStatus('inviteStatus', inv.HasPin
            ? 'Settings copied into the form. Set a new PIN, then create the invite.'
            : 'Settings copied into the form. Review and create the invite.', false);
    }

    function createInvite() {
        var setExpiry = !!(Shared.getEl('chkSetExpiry') || {}).checked;
        var expVal = (Shared.getEl('dateExpiry') || {}).value || '';
        var setMaxUses = !!(Shared.getEl('chkSetMaxUses') || {}).checked;
        var maxUses = parseInt(Shared.getEl('txtMaxUses').value, 10);
        var groupId = Shared.getEl('selGroup').value || null;

        if (!groupId) {
            Shared.setStatus('inviteStatus', _groups.length ? 'Choose a group for this invite.' : 'Create a group on the Groups tab first.', true);
            return;
        }
        if (setExpiry && !expVal) {
            Shared.setStatus('inviteStatus', 'Choose an expiration date or turn off the expiration.', true);
            return;
        }
        if (setMaxUses && (isNaN(maxUses) || maxUses < 1)) {
            Shared.setStatus('inviteStatus', 'Maximum usages must be 1 or higher.', true);
            return;
        }

        var pin = (Shared.getEl('txtPin').value || '').trim();
        if (pin && !/^\d{6}$/.test(pin)) {
            Shared.setStatus('inviteStatus', 'The PIN must be exactly 6 digits, like a Quick Connect code.', true);
            return;
        }

        var payload = {
            Label: Shared.getEl('txtLabel').value || '',
            Message: Shared.getEl('txtMessage').value || '',
            Resources: _newResources.slice(),
            Pin: pin,
            UseDefaultGroup: false,
            GroupId: groupId,
            ExpiresAt: (setExpiry && expVal) ? expVal + 'T00:00:00' : null,
            MaxUses: (setMaxUses && maxUses >= 1) ? maxUses : 0
        };

        Shared.apiRequest('Invites', 'POST', payload)
            .then(function () {
                Shared.setStatus('inviteStatus', 'Invite created.', false);
                Shared.getEl('txtLabel').value = '';
                Shared.getEl('txtMessage').value = '';
                _newResources = [];
                renderResourceList();
                Shared.getEl('txtPin').value = '';
                Shared.getEl('chkSetExpiry').checked = false;
                Shared.getEl('dateExpiry').value = '';
                Shared.getEl('chkSetMaxUses').checked = false;
                Shared.getEl('txtMaxUses').value = '1';
                updateExpiryState();
                updateMaxUsesState();
                loadAll();
            })
            .catch(function () {
                Shared.setStatus('inviteStatus', 'Failed to create invite.', true);
            });
    }

    function updateHttpsWarning() {
        var v = ((Shared.getEl('txtInviteBaseUrl') || {}).value || '').trim().toLowerCase();
        var insecure = v.indexOf('http://') === 0 || (v === '' && location.protocol === 'http:');
        Shared.setVisible('httpsWarn', insecure);
    }

    function saveBaseUrl() {
        Shared.getConfig().then(function (config) {
            config.InviteBaseUrl = ((Shared.getEl('txtInviteBaseUrl') || {}).value || '').trim().replace(/\/+$/, '');
            var rc = parseInt((Shared.getEl('txtRateCount') || {}).value, 10);
            var rw = parseInt((Shared.getEl('txtRateWindow') || {}).value, 10);
            config.InviteRateLimitCount = isNaN(rc) || rc < 0 ? 0 : rc;
            config.InviteRateLimitWindowMinutes = isNaN(rw) || rw < 0 ? 0 : rw;
            Shared.saveConfig(config).then(function () {
                _base = config.InviteBaseUrl;
                Shared.setStatus('inviteUrlStatus', 'Invite settings saved.', false);
                updateHttpsWarning();
                renderInvites();
            }).catch(function () {
                Shared.setStatus('inviteUrlStatus', 'Failed to save invite settings.', true);
            });
        });
    }

    function setInviteExpiry(id, dateStr) {
        var expiresAt = dateStr ? dateStr + 'T00:00:00' : null;
        Shared.apiRequest('Invites/' + id + '/Expiry', 'POST', { ExpiresAt: expiresAt })
            .then(function () {
                Shared.setStatus('inviteStatus', 'Invite expiry updated.', false);
                loadAll();
            })
            .catch(function () {
                Shared.setStatus('inviteStatus', 'Failed to update expiry.', true);
            });
    }

    function setInviteEnabled(id, enabled) {
        // Mirror the server's enable guards with specific messages. The server enforces them too.
        if (enabled) {
            var inv = _invites.filter(function (i) { return i.Id === id; })[0];
            if (inv) {
                if (inv.ExpiresAt && new Date(inv.ExpiresAt) <= new Date()) {
                    Shared.setStatus('inviteStatus', 'This invite has expired. Move its expiration forward to re-enable it.', true);
                    return;
                }
                var gid = inv.UseDefaultGroup ? _defaultGroupId : inv.GroupId;
                var group = _groups.filter(function (g) { return g.Id === gid; })[0];
                if (group && blocksInvites(group)) {
                    Shared.setStatus('inviteStatus', "This invite's group disallows all password changes, so it cannot be enabled.", true);
                    return;
                }
            }
        }

        Shared.apiRequest('Invites/' + id + '/Enabled', 'POST', { Enabled: enabled })
            .then(function () {
                Shared.setStatus('inviteStatus', enabled ? 'Invite enabled.' : 'Invite disabled.', false);
                loadAll();
            })
            .catch(function () {
                Shared.setStatus('inviteStatus', 'Failed to update invite.', true);
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
        var burl = Shared.getEl('btnSaveInviteUrl');
        if (burl) burl.addEventListener('click', saveBaseUrl);
        var urlInput = Shared.getEl('txtInviteBaseUrl');
        if (urlInput) urlInput.addEventListener('input', updateHttpsWarning);
        var chkExp = Shared.getEl('chkSetExpiry');
        if (chkExp) chkExp.addEventListener('change', updateExpiryState);
        var chkMax = Shared.getEl('chkSetMaxUses');
        if (chkMax) chkMax.addEventListener('change', updateMaxUsesState);
        var btnRes = Shared.getEl('btnAddResource');
        if (btnRes) btnRes.addEventListener('click', addResource);
        var resList = Shared.getEl('resList');
        if (resList) resList.addEventListener('click', function (e) {
            var del = e.target.closest('.um-res-del');
            if (!del) return;
            _newResources.splice(parseInt(del.getAttribute('data-index'), 10), 1);
            renderResourceList();
        });
        updateExpiryState();
        updateMaxUsesState();
    });
}
