// usermanagement_invites.js
// The Invites tab, which creates invite links, sets the invite Server URL and rate limit, and lists every invite.

import { createPage, createKeyValueEditor, createCopyField, confirmDialog } from './configurationpage?name=usermanagement_shared.js';

// Invites page
// Builds the Invites tab.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
export default function (view) {
    'use strict';

    var Shared = null;
    var _resourceEditor = null;
    var _copyFields = [];
    var _invites = [];
    var _groups = [];
    var _base = '';
    var _defaultGroupId = '';

    // inviteUrl
    // Builds an invite's public link from the Server URL, or the server's own address when it is blank.
    //
    // Inputs
    // token [string]: The invite's token.
    //
    // Output
    // [string]: The link.
    function inviteUrl(token) {
        var base = (_base || ApiClient.serverAddress() || '').replace(/\/+$/, '');
        return base + '/invite/' + token;
    }

    // groupName
    // Looks up a group's name.
    //
    // Inputs
    // id [string]: The group id.
    //
    // Output
    // [string]: The name, or empty when the group is gone.
    function groupName(id) {
        for (var i = 0; i < _groups.length; i++) {
            if (_groups[i].Id === id) return _groups[i].Name;
        }
        return '';
    }

    // groupExpired
    // Says whether a group has reached its expiration. The server counts whole UTC days, so only the dates are compared.
    //
    // Inputs
    // g [object]: The group, or null.
    //
    // Output
    // [boolean]: Whether it has expired.
    function groupExpired(g) {
        return !!g && dateReached(g.ExpiresOn);
    }

    // inviteExpired
    // Says whether an invite has reached its expiration, on the same UTC day the server starts refusing it.
    //
    // Inputs
    // inv [object]: The invite, or null.
    //
    // Output
    // [boolean]: Whether it has expired.
    function inviteExpired(inv) {
        return !!inv && dateReached(inv.ExpiresAt);
    }

    // dateReached
    // Says whether today, in UTC, is on or after a stored date.
    //
    // Inputs
    // value [string]: The stored date, or empty.
    //
    // Output
    // [boolean]: Whether the date has been reached.
    function dateReached(value) {
        if (!value) return false;
        return String(value).slice(0, 10) <= new Date().toISOString().slice(0, 10);
    }

    // showError
    // Shows the reason the server gave for a refused request, such as an expired invite, or the fallback.
    //
    // Inputs
    // statusId [string]: The status element.
    // err [object]: The failed response.
    // fallback [string]: The message when the server gave none.
    function showError(statusId, err, fallback) {
        Shared.errorText(err, fallback).then(function (text) {
            Shared.setStatus(statusId, text, true);
        });
    }

    // inviteGroup
    // Finds the group an invite signs users into, following the default group when the invite does.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [object]: The group, or null.
    function inviteGroup(inv) {
        var gid = inv.UseDefaultGroup ? _defaultGroupId : inv.GroupId;
        return _groups.filter(function (g) { return g.Id === gid; })[0] || null;
    }

    // statusOf
    // Picks an invite's status for its badge and the count cards.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [object]: { cls, text }, where cls is Active, Locked, UsedUp, or Expired.
    function statusOf(inv) {
        if (inviteExpired(inv)) return { cls: 'Expired', text: 'Expired' };
        if (!inv.Enabled) return { cls: 'Locked', text: 'Locked' };
        if (inv.MaxUses > 0 && inv.UsedCount >= inv.MaxUses) return { cls: 'UsedUp', text: 'Used up' };
        // The server refuses signups into an expired group, so the link is dead until the group's
        // expiration moves even though the invite itself is still enabled.
        if (groupExpired(inviteGroup(inv))) return { cls: 'Expired', text: 'Group expired' };
        return { cls: 'Active', text: 'Active' };
    }

    // statusBadge
    // Draws an invite's status badge. Used up has no Base state, so it takes the gray badge for off.
    //
    // Inputs
    // st [object]: The status from statusOf.
    //
    // Output
    // [string]: The badge HTML.
    function statusBadge(st) {
        if (st.cls === 'UsedUp') return Shared.badge('gray', st.text);
        return Shared.statusBadge(st.text, st.cls);
    }

    // loadAll
    // Reads the invites and the settings, then draws the form and the list.
    function loadAll() {
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
    }

    // ensureResourceEditor
    // Draws the Resources editor once, as a Key Value Editor of titles and URLs.
    function ensureResourceEditor() {
        if (_resourceEditor) return;
        _resourceEditor = createKeyValueEditor({
            keyLabel: 'Title',
            valueLabel: 'URL',
            keyPlaceholder: 'Request shows',
            valuePlaceholder: 'https://requests.example.com',
            addText: 'Add Resource',
            requiredText: 'Enter a title.',
            duplicateKeys: true,
            validate: function (key, value) {
                if (!key && !value) return '';
                if (!/^https?:\/\/.+/i.test((value || '').trim())) return 'Enter a full http or https URL.';
                return '';
            }
        });
        Shared.getEl('resourceMount').appendChild(_resourceEditor.element);
    }

    // readResources
    // Reads the Resources editor as the API's resource list.
    //
    // Output
    // [Array]: One { Title, Url } per filled row.
    function readResources() {
        return _resourceEditor.getValue().map(function (kv) {
            return { Title: (kv.key || '').trim(), Url: (kv.value || '').trim() };
        });
    }

    // blocksInvites
    // Says whether a group disallows all password changes. Such groups are admin managed and cannot take invites,
    // so they are left out of the select. The server refuses them too.
    //
    // Inputs
    // g [object]: The group.
    //
    // Output
    // [boolean]: Whether it blocks invites.
    function blocksInvites(g) {
        var pw = g.Password || {};
        var mode = typeof pw.ChangeMode === 'number' ? pw.ChangeMode === 2 : pw.ChangeMode === 'Disallowed';
        return pw.Enabled === true && mode;
    }

    // Sentinel option value: the invite follows whatever group is the default at redeem time rather
    // than pinning one, which is what UseDefaultGroup means on the server.
    var DEFAULT_OPTION = '__default__';

    // defaultGroup
    // Finds the default group when it can take invites.
    //
    // Output
    // [object]: The group, or null.
    function defaultGroup() {
        if (!_defaultGroupId) return null;
        var found = _groups.filter(function (g) { return g.Id === _defaultGroupId; })[0];
        return (found && !blocksInvites(found)) ? found : null;
    }

    // populateGroups
    // Fills the group select, with the default group first when there is one.
    function populateGroups() {
        var sel = Shared.getEl('selGroup');
        if (!sel) return;
        var def = defaultGroup();
        sel.innerHTML = '';

        if (def) {
            var defOpt = document.createElement('option');
            defOpt.value = DEFAULT_OPTION;
            defOpt.textContent = 'Default group (' + (def.Name || 'Unnamed group') + ')' + (groupExpired(def) ? ' (expired)' : '');
            sel.appendChild(defOpt);
        }

        _groups.forEach(function (g) {
            if (blocksInvites(g)) return;
            var opt = document.createElement('option');
            opt.value = g.Id;
            opt.textContent = (g.Name || 'Unnamed group') + (g.Id === _defaultGroupId ? ' (Default)' : '') + (groupExpired(g) ? ' (expired)' : '');
            sel.appendChild(opt);
        });

        if (def) {
            sel.value = DEFAULT_OPTION;
        } else if (_defaultGroupId && _groups.some(function (g) { return g.Id === _defaultGroupId; })) {
            sel.value = _defaultGroupId;
        }
        updateGroupWarning();
    }

    // selectedGroup
    // Reads the group picked for a new invite.
    //
    // Output
    // [object]: The group, or null.
    function selectedGroup() {
        var value = (Shared.getEl('selGroup') || {}).value || '';
        if (!value) return null;
        if (value === DEFAULT_OPTION) return defaultGroup();
        return _groups.filter(function (g) { return g.Id === value; })[0] || null;
    }

    // updateGroupWarning
    // Shows the warning when the picked group has expired.
    function updateGroupWarning() {
        Shared.setVisible('groupExpiredWarn', groupExpired(selectedGroup()));
    }

    // renderInviteCards
    // Draws the count cards, one per status, in the badge colors.
    function renderInviteCards() {
        var active = 0, expired = 0, locked = 0, consumed = 0;
        _invites.forEach(function (inv) {
            var cls = statusOf(inv).cls;
            if (cls === 'Expired') { expired++; }
            else if (cls === 'Locked') { locked++; }
            else if (cls === 'UsedUp') { consumed++; }
            else { active++; }
        });
        Shared.renderCards('inviteCards', [
            { count: active, label: 'Active', color: 'green' },
            { count: locked, label: 'Locked', color: 'purple' },
            { count: consumed, label: 'Consumed', color: 'gray' },
            { count: expired, label: 'Expired', color: 'orange' }
        ]);
    }

    // clearCopyFields
    // Tears down the link Copy Fields of the last draw.
    function clearCopyFields() {
        _copyFields.forEach(function (f) { f.destroy(); });
        _copyFields = [];
    }

    // renderInvites
    // Draws the count cards and one row per invite, with its link, status, actions, and expiration.
    function renderInvites() {
        renderInviteCards();
        clearCopyFields();
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
            if (inv.UseDefaultGroup) meta.push('Group: Default' + (_defaultGroupId ? ' (' + esc(groupName(_defaultGroupId)) + ')' : ''));
            else if (inv.GroupId) meta.push('Group: ' + esc(groupName(inv.GroupId)));
            if (inv.HasPin) meta.push('PIN set');
            if (groupExpired(inviteGroup(inv))) meta.push('<span class="um-text-red">Group has expired. Signups are refused until its expiration is moved.</span>');

            var toggleIcon = inv.Enabled ? 'lock' : 'lock_open';
            var toggleTitle = inv.Enabled ? 'Disable Invite' : 'Enable Invite';

            html += '<div class="base-table-row um-invite-row" data-id="' + esc(inv.Id) + '">'
                + '<div class="um-row">'
                + '<div class="base-table-item-info um-row-info">'
                + '<div class="base-table-item-title">' + (esc(inv.Label) || 'Untitled invite') + '</div>'
                + '<div class="base-table-item-sub">' + meta.join(' • ') + '</div>'
                + '</div>'
                + statusBadge(st)
                + '<button type="button" class="base-row-btn um-dup" title="Duplicate" aria-label="Duplicate"><span class="material-icons" aria-hidden="true">content_copy</span></button>'
                + '<button type="button" class="base-row-btn um-toggle" title="' + toggleTitle + '" aria-label="' + toggleTitle + '"><span class="material-icons" aria-hidden="true">' + toggleIcon + '</span></button>'
                + '<button type="button" class="base-row-btn destructive um-del" title="Delete" aria-label="Delete"><span class="material-icons" aria-hidden="true">delete</span></button>'
                + '</div>'
                + '<div class="um-url-mount" data-url="' + esc(url) + '"></div>'
                + '<div class="um-row">'
                + '<label class="um-row-label" for="expiry-' + esc(inv.Id) + '">Expires</label>'
                + '<input type="date" id="expiry-' + esc(inv.Id) + '" class="base-edit-input um-expiry" value="' + (inv.ExpiresAt ? esc(String(inv.ExpiresAt).slice(0, 10)) : '') + '" />'
                + '</div>'
                + '</div>';
        });

        body.innerHTML = '<div class="um-table">' + html + '</div>';
        if (footer) footer.textContent = Shared.plural(_invites.length, 'invite');

        body.querySelectorAll('.um-invite-row').forEach(function (row) {
            var id = row.getAttribute('data-id');
            var del = row.querySelector('.um-del');
            var mount = row.querySelector('.um-url-mount');
            if (mount) {
                var field = createCopyField({ value: mount.getAttribute('data-url'), label: 'Invite link', copiedText: 'Link copied.' });
                mount.appendChild(field.element);
                _copyFields.push(field);
            }
            var toggle = row.querySelector('.um-toggle');
            if (toggle) toggle.addEventListener('click', function () {
                var inv = _invites.filter(function (x) { return String(x.Id) === String(id); })[0];
                setInviteEnabled(id, !(inv && inv.Enabled));
            });
            var expiryInput = row.querySelector('.um-expiry');
            if (expiryInput) expiryInput.addEventListener('change', function () { setInviteExpiry(id, this.value); });
            if (del) del.addEventListener('click', function () { deleteInvite(id); });
            var dup = row.querySelector('.um-dup');
            if (dup) dup.addEventListener('click', function () { duplicateInvite(id); });
        });
    }

    // updateExpiryState
    // Shows the expiration date while its checkbox is on.
    function updateExpiryState() {
        Shared.setVisible('expiryFields', !!(Shared.getEl('chkSetExpiry') || {}).checked);
    }

    // updateMaxUsesState
    // Shows the usage count while its checkbox is on.
    function updateMaxUsesState() {
        Shared.setVisible('maxUsesFields', !!(Shared.getEl('chkSetMaxUses') || {}).checked);
    }

    // openCreateSection
    // Opens the Create Invite section and scrolls to it.
    function openCreateSection() {
        var header = view.querySelector('.base-collapsible-header[data-target="newInviteContent"]');
        var content = Shared.getEl('newInviteContent');
        if (header && content && content.classList.contains('collapsed')) {
            header.click();
        }
        if (content && content.scrollIntoView) {
            content.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    // duplicateInvite
    // Copies an invite's settings into the Create Invite form. The PIN is stored hashed, so it is left blank.
    //
    // Inputs
    // id [string]: The invite to copy.
    function duplicateInvite(id) {
        var inv = _invites.filter(function (x) { return String(x.Id) === String(id); })[0];
        if (!inv) return;

        Shared.getEl('txtLabel').value = inv.Label ? inv.Label + ' copy' : '';
        Shared.getEl('txtMessage').value = inv.Message || '';

        _resourceEditor.setValue((inv.Resources || []).map(function (r) {
            return { key: r.Title, value: r.Url };
        }));

        var sel = Shared.getEl('selGroup');
        if (sel) {
            var wanted = inv.UseDefaultGroup ? DEFAULT_OPTION : inv.GroupId;
            var hasOption = Array.prototype.some.call(sel.options, function (o) { return o.value === wanted; });
            if (hasOption) sel.value = wanted;
        }

        var hasExpiry = !!inv.ExpiresAt;
        Shared.getEl('chkSetExpiry').checked = hasExpiry;
        Shared.getEl('dateExpiry').value = hasExpiry ? String(inv.ExpiresAt).slice(0, 10) : '';
        updateExpiryState();

        var hasMaxUses = inv.MaxUses > 0;
        Shared.getEl('chkSetMaxUses').checked = hasMaxUses;
        Shared.getEl('txtMaxUses').value = hasMaxUses ? String(inv.MaxUses) : '1';
        updateMaxUsesState();

        Shared.getEl('txtPin').value = '';

        openCreateSection();
        Shared.setStatus('inviteStatus', inv.HasPin
            ? 'Settings copied into the form. Set a new PIN, then create the invite.'
            : 'Settings copied into the form. Review and create the invite.', false);
    }

    // createInvite
    // Checks the form and creates the invite.
    function createInvite() {
        var setExpiry = !!(Shared.getEl('chkSetExpiry') || {}).checked;
        var expVal = (Shared.getEl('dateExpiry') || {}).value || '';
        var setMaxUses = !!(Shared.getEl('chkSetMaxUses') || {}).checked;
        var maxUses = parseInt(Shared.getEl('txtMaxUses').value, 10);
        var groupId = Shared.getEl('selGroup').value || null;

        if (!groupId) {
            Shared.setStatus('inviteStatus', _groups.length ? 'Select a group for this invite.' : 'Create a group on the Groups tab first.', true);
            return;
        }
        if (setExpiry && !expVal) {
            Shared.setStatus('inviteStatus', 'Select an expiration date or turn off the expiration.', true);
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

        if (!_resourceEditor.isValid()) {
            Shared.setStatus('inviteStatus', 'Each resource needs a title and a full http or https URL.', true);
            return;
        }

        var useDefault = groupId === DEFAULT_OPTION;
        var payload = {
            Label: Shared.getEl('txtLabel').value || '',
            Message: Shared.getEl('txtMessage').value || '',
            Resources: readResources(),
            Pin: pin,
            UseDefaultGroup: useDefault,
            GroupId: useDefault ? null : groupId,
            ExpiresAt: (setExpiry && expVal) ? expVal + 'T00:00:00' : null,
            MaxUses: (setMaxUses && maxUses >= 1) ? maxUses : 0
        };

        var expiredGroup = groupExpired(selectedGroup());
        Shared.apiRequest('Invites', 'POST', payload)
            .then(function () {
                Shared.setStatus('inviteStatus', expiredGroup
                    ? 'Invite created, but its group has expired. The link does not work until the group expiration is moved or cleared.'
                    : 'Invite created.', expiredGroup);
                Shared.getEl('txtLabel').value = '';
                Shared.getEl('txtMessage').value = '';
                _resourceEditor.setValue([]);
                Shared.getEl('txtPin').value = '';
                Shared.getEl('chkSetExpiry').checked = false;
                Shared.getEl('dateExpiry').value = '';
                Shared.getEl('chkSetMaxUses').checked = false;
                Shared.getEl('txtMaxUses').value = '1';
                updateExpiryState();
                updateMaxUsesState();
                loadAll();
            })
            .catch(function (err) {
                showError('inviteStatus', err, 'Could not create the invite.');
            });
    }

    // updateHttpsWarning
    // Shows the warning while invite links would use plain http.
    function updateHttpsWarning() {
        var v = ((Shared.getEl('txtInviteBaseUrl') || {}).value || '').trim().toLowerCase();
        var insecure = v.indexOf('http://') === 0 || (v === '' && location.protocol === 'http:');
        Shared.setVisible('httpsWarn', insecure);
    }

    // saveBaseUrl
    // Saves the Server URL and the rate limit.
    function saveBaseUrl() {
        Shared.getConfig().then(function (config) {
            config.InviteBaseUrl = ((Shared.getEl('txtInviteBaseUrl') || {}).value || '').trim().replace(/\/+$/, '');
            var rc = parseInt((Shared.getEl('txtRateCount') || {}).value, 10);
            var rw = parseInt((Shared.getEl('txtRateWindow') || {}).value, 10);
            config.InviteRateLimitCount = isNaN(rc) || rc < 0 ? 0 : rc;
            config.InviteRateLimitWindowMinutes = isNaN(rw) || rw < 0 ? 0 : rw;
            return Shared.saveConfig(config).then(function () {
                _base = config.InviteBaseUrl;
                Shared.setStatus('inviteUrlStatus', 'Invite settings saved.', false);
                updateHttpsWarning();
                renderInvites();
            });
        }).catch(function () {
            Shared.setStatus('inviteUrlStatus', 'Could not save the invite settings.', true);
        });
    }

    // setInviteExpiry
    // Saves an invite's expiration from its row.
    //
    // Inputs
    // id [string]: The invite.
    // dateStr [string]: The new date, or empty for none.
    function setInviteExpiry(id, dateStr) {
        var expiresAt = dateStr ? dateStr + 'T00:00:00' : null;
        Shared.apiRequest('Invites/' + id + '/Expiry', 'POST', { ExpiresAt: expiresAt })
            .then(function () {
                Shared.setStatus('inviteStatus', 'Invite expiry updated.', false);
                loadAll();
            })
            .catch(function (err) {
                showError('inviteStatus', err, 'Could not update the expiry.');
            });
    }

    // setInviteEnabled
    // Enables or disables an invite, with the server's enable guards repeated here for clearer messages.
    //
    // Inputs
    // id [string]: The invite.
    // enabled [boolean]: The new state.
    function setInviteEnabled(id, enabled) {
        if (enabled) {
            var inv = _invites.filter(function (i) { return i.Id === id; })[0];
            if (inv) {
                if (inviteExpired(inv)) {
                    Shared.setStatus('inviteStatus', 'This invite has expired. Move its expiration forward to re-enable it.', true);
                    return;
                }
                var group = inviteGroup(inv);
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
            .catch(function (err) {
                showError('inviteStatus', err, 'Could not update the invite.');
            });
    }

    // deleteInvite
    // Asks first, then deletes an invite.
    //
    // Inputs
    // id [string]: The invite.
    function deleteInvite(id) {
        confirmDialog('Delete this invite? The link stops working.', {
            title: 'Delete Invite',
            confirmText: 'Delete',
            destructive: true
        }).then(function (ok) {
            if (!ok) return;
            Shared.apiRequest('Invites/' + id, 'DELETE')
                .then(function () {
                    Shared.setStatus('inviteStatus', 'Invite deleted.', false);
                    loadAll();
                })
                .catch(function (err) {
                    showError('inviteStatus', err, 'Could not delete the invite.');
                });
        });
    }

    // bind
    // Wires the form once and draws the Resources editor.
    function bind() {
        Shared.initCollapsibles();
        ensureResourceEditor();
        var btn = Shared.getEl('btnCreateInvite');
        if (btn) btn.addEventListener('click', createInvite);
        var burl = Shared.getEl('btnSaveInviteUrl');
        if (burl) burl.addEventListener('click', saveBaseUrl);
        var urlInput = Shared.getEl('txtInviteBaseUrl');
        if (urlInput) urlInput.addEventListener('input', updateHttpsWarning);
        var selGroup = Shared.getEl('selGroup');
        if (selGroup) selGroup.addEventListener('change', updateGroupWarning);
        var chkExp = Shared.getEl('chkSetExpiry');
        if (chkExp) chkExp.addEventListener('change', updateExpiryState);
        var chkMax = Shared.getEl('chkSetMaxUses');
        if (chkMax) chkMax.addEventListener('change', updateMaxUsesState);
        updateExpiryState();
        updateMaxUsesState();
    }

    Shared = createPage(view, 1, { bind: bind, load: loadAll, onHide: clearCopyFields }).shared;
}
