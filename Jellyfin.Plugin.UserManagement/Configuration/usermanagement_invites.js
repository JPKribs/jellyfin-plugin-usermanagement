// usermanagement_invites.js
// The Invites tab, which creates invite links, sets the invite Server URL and rate limit, and lists every invite.

import {
    createPage,
    createKeyValueEditor,
    createCopyField,
    createRecordList,
    createDefinitionList,
    openEditorDialog,
    confirmDialog,
    createSaveBar
} from './configurationpage?name=usermanagement_shared.js';

// Invites page
// Builds the Invites tab.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
export default function (view) {
    'use strict';

    var Shared = null;
    var Page = null;
    var _resourceEditor = null;
    var _inviteList = null;
    var _invites = [];
    var _groups = [];
    var _base = '';
    var _defaultGroupId = '';
    var _urlSaveBar = null;

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

    var STATUS_LOOK = {
        Active: { color: 'green', icon: 'check_circle' },
        Locked: { color: 'purple', icon: 'lock' },
        UsedUp: { color: 'gray', icon: 'block' },
        Expired: { color: 'orange', icon: 'event_busy' }
    };

    // statusOf
    // Picks an invite's status for its tag, its badge, and the count cards.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [object]: { cls, text }, where cls is Active, Locked, UsedUp, or Expired.
    function statusOf(inv) {
        if (inviteExpired(inv)) return { cls: 'Expired', text: 'Expired' };
        if (!inv.Enabled) return { cls: 'Locked', text: 'Locked' };
        if (inv.MaxUses > 0 && inv.UsedCount >= inv.MaxUses) return { cls: 'UsedUp', text: 'Used Up' };
        // The server refuses signups into an expired group, so the link is dead until the group's
        // expiration moves even though the invite itself is still enabled.
        if (groupExpired(inviteGroup(inv))) return { cls: 'Expired', text: 'Group Expired' };
        return { cls: 'Active', text: 'Active' };
    }

    // usesText
    // Says how many times an invite was used, out of its limit when it has one.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [string]: Such as "1 use of 5" or "3 uses".
    function usesText(inv) {
        return Shared.plural(inv.UsedCount || 0, 'use') + (inv.MaxUses > 0 ? ' of ' + inv.MaxUses : '');
    }

    // groupText
    // Names the group an invite signs users into.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [string]: The group's name, or Default group with the name it follows.
    function groupText(inv) {
        if (inv.UseDefaultGroup) return 'Default group' + (_defaultGroupId ? ' (' + groupName(_defaultGroupId) + ')' : '');
        return inv.GroupId ? groupName(inv.GroupId) : '';
    }

    // expiryDate
    // Reads an invite's expiration as a date input value.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [string]: The date as YYYY-MM-DD, or empty for none.
    function expiryDate(inv) {
        return inv.ExpiresAt ? String(inv.ExpiresAt).slice(0, 10) : '';
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
            if (_urlSaveBar) _urlSaveBar.setDirty(false);
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

    // copyLinkButton
    // Builds the row button that copies an invite's link and shows a check after the copy.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [HTMLElement]: The button.
    function copyLinkButton(inv) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'base-row-btn';
        btn.title = 'Copy Link';
        btn.setAttribute('aria-label', 'Copy link of ' + (inv.Label || 'Untitled invite'));
        btn.innerHTML = '<span class="material-icons" aria-hidden="true">content_copy</span>';
        btn.addEventListener('click', function () {
            Shared.copyToClipboard(inviteUrl(inv.Token)).then(function (ok) {
                Shared.setStatus('inviteStatus', ok === false ? 'Could not copy the link.' : 'Link copied.', ok === false);
                if (ok === false) return;
                btn.classList.add('success');
                setTimeout(function () { btn.classList.remove('success'); }, 1500);
            });
        });
        return btn;
    }

    // toItem
    // Builds one Record List row, with the invite's uses, group, status, and expiration, and a Copy Link button.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [object]: The Record List item.
    function toItem(inv) {
        var st = statusOf(inv);
        var parts = [usesText(inv), groupText(inv)];
        if (inv.HasPin) parts.push('PIN set');
        var tags = [{ label: st.text, icon: STATUS_LOOK[st.cls].icon }];
        if (inv.ExpiresAt) tags.push({ label: 'Expires ' + expiryDate(inv), icon: 'event' });
        return {
            id: String(inv.Id),
            inviteId: inv.Id,
            title: inv.Label || 'Untitled invite',
            secondary: parts.filter(Boolean).join(' · '),
            tags: tags,
            aside: copyLinkButton(inv)
        };
    }

    // ensureInviteList
    // Draws the Record List once.
    function ensureInviteList() {
        if (_inviteList) return;
        _inviteList = createRecordList({
            label: 'Invites',
            emptyText: 'No invites yet. Create one above.',
            openLabel: 'Open {value}',
            onOpen: function (item) { openInvite(item.inviteId); }
        });
        Shared.getEl('inviteList').appendChild(_inviteList.element);
    }

    // destroyInviteList
    // Tears down the Record List when the page hides.
    function destroyInviteList() {
        if (!_inviteList) return;
        _inviteList.destroy();
        if (_inviteList.element.parentNode) _inviteList.element.parentNode.removeChild(_inviteList.element);
        _inviteList = null;
    }

    // renderInvites
    // Draws the count cards and one row per invite.
    function renderInvites() {
        renderInviteCards();
        ensureInviteList();
        _inviteList.setItems(_invites.map(toItem));
    }

    // findInvite
    // Looks up a loaded invite.
    //
    // Inputs
    // id [string]: The invite.
    //
    // Output
    // [object]: The invite, or undefined.
    function findInvite(id) {
        return _invites.filter(function (x) { return String(x.Id) === String(id); })[0];
    }

    // openInvite
    // Opens an invite in an editor dialog with its link, its details, its expiration, and whether it accepts signups.
    // Save writes what changed, Delete asks first, and Duplicate copies it into the Create Invite form.
    //
    // Inputs
    // id [string]: The invite.
    function openInvite(id) {
        var inv = findInvite(id);
        if (!inv) return;
        var st = statusOf(inv);
        var body = document.createElement('div');
        body.innerHTML = '<div class="um-setting"><label class="inputLabel" for="inviteLinkField">Invite link</label><div data-mount="link"></div></div>'
            + '<div data-mount="details"></div>'
            + '<div class="inputContainer"><label class="inputLabel" for="inviteExpiryField">Invite expiration</label>'
            + '<input is="emby-input" type="date" id="inviteExpiryField" />'
            + '<div class="fieldDescription">Leave blank for no expiration.</div></div>'
            + '<div class="checkboxContainer checkboxContainer-withDescription"><label class="emby-checkbox-label">'
            + '<input type="checkbox" is="emby-checkbox" id="inviteEnabledField" /><span class="checkboxLabel">Accept signups</span></label>'
            + '<div class="fieldDescription checkboxFieldDescription">Turn off to lock the link. Turn on to unlock it after too many wrong PIN tries.</div></div>'
            + '<button is="emby-button" type="button" class="raised base-button secondary small" id="inviteDuplicate">'
            + '<span class="material-icons" aria-hidden="true">content_copy</span><span>Duplicate</span></button>'
            + '<div><span class="base-status-text" id="inviteDialogStatus" role="status"></span></div>';

        var link = createCopyField({ value: inviteUrl(inv.Token), label: 'Invite link', copiedText: 'Link copied.' });
        link.input.id = 'inviteLinkField';
        body.querySelector('[data-mount="link"]').appendChild(link.element);
        var details = createDefinitionList({
            items: [
                { label: 'Status', value: { badge: { label: st.text, color: STATUS_LOOK[st.cls].color } } },
                { label: 'Uses', value: usesText(inv) },
                { label: 'Group', value: groupText(inv) },
                { label: 'PIN', value: inv.HasPin ? 'Set' : 'None' }
            ]
        });
        body.querySelector('[data-mount="details"]').appendChild(details.element);

        var expiryInput = body.querySelector('#inviteExpiryField');
        var enabledInput = body.querySelector('#inviteEnabledField');
        expiryInput.value = expiryDate(inv);
        enabledInput.checked = !!inv.Enabled;

        var dialog = openEditorDialog({
            title: inv.Label || 'Untitled invite',
            body: body,
            className: 'pluginConfigurationPage',
            size: 'medium',
            page: Page,
            doneText: 'Save',
            removeText: 'Delete',
            serialize: function () { return expiryInput.value + '|' + enabledInput.checked; },
            onDone: function () { return saveInvite(inv, expiryInput.value, enabledInput.checked); },
            onRemove: function (close) { deleteInvite(inv.Id).then(function (ok) { if (ok) close(); }); },
            onClose: function () { link.destroy(); details.destroy(); }
        });

        body.querySelector('#inviteDuplicate').addEventListener('click', function () {
            dialog.markClean();
            dialog.close();
            duplicateInvite(inv.Id);
        });
    }

    // saveInvite
    // Writes an invite's new expiration and state, with the server's enable guards repeated here for clearer messages.
    //
    // Inputs
    // inv [object]: The invite as loaded.
    // dateStr [string]: The new expiration date, or empty for none.
    // enabled [boolean]: Whether it accepts signups.
    //
    // Output
    // [Promise]: false to keep the dialog open.
    function saveInvite(inv, dateStr, enabled) {
        var expiryChanged = dateStr !== expiryDate(inv);
        var enabledChanged = enabled !== !!inv.Enabled;
        if (enabled && enabledChanged) {
            if (dateReached(dateStr)) {
                Shared.setStatus('inviteDialogStatus', 'This invite has expired. Move its expiration forward to enable it.', true);
                return Promise.resolve(false);
            }
            var group = inviteGroup(inv);
            if (group && blocksInvites(group)) {
                Shared.setStatus('inviteDialogStatus', 'This invite\'s group disallows all password changes, so it cannot be enabled.', true);
                return Promise.resolve(false);
            }
        }

        var chain = Promise.resolve();
        if (expiryChanged) {
            chain = chain.then(function () {
                return Shared.apiRequest('Invites/' + inv.Id + '/Expiry', 'POST', { ExpiresAt: dateStr ? dateStr + 'T00:00:00' : null });
            });
        }
        if (enabledChanged) {
            chain = chain.then(function () {
                return Shared.apiRequest('Invites/' + inv.Id + '/Enabled', 'POST', { Enabled: enabled });
            });
        }
        return chain.then(function () {
            if (expiryChanged || enabledChanged) Shared.setStatus('inviteStatus', 'Invite saved.', false);
            loadAll();
            return true;
        }).catch(function (err) {
            Shared.errorText(err, 'Could not save the invite.').then(function (text) {
                Shared.setStatus('inviteDialogStatus', text, true);
            });
            loadAll();
            return false;
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
        var inv = findInvite(id);
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
    //
    // Output
    // [Promise]: Settles when the save is done, and rejects when it fails.
    function saveBaseUrl() {
        return Shared.getConfig().then(function (config) {
            config.InviteBaseUrl = ((Shared.getEl('txtInviteBaseUrl') || {}).value || '').trim().replace(/\/+$/, '');
            var rc = parseInt((Shared.getEl('txtRateCount') || {}).value, 10);
            var rw = parseInt((Shared.getEl('txtRateWindow') || {}).value, 10);
            config.InviteRateLimitCount = isNaN(rc) || rc < 0 ? 0 : rc;
            config.InviteRateLimitWindowMinutes = isNaN(rw) || rw < 0 ? 0 : rw;
            return Shared.saveConfig(config).then(function () {
                _base = config.InviteBaseUrl;
                updateHttpsWarning();
                renderInvites();
            });
        }).catch(function () {
            throw new Error('Could not save the invite settings.');
        });
    }

    // deleteInvite
    // Asks first, then deletes an invite.
    //
    // Inputs
    // id [string]: The invite.
    //
    // Output
    // [Promise]: Whether the invite was deleted.
    function deleteInvite(id) {
        return confirmDialog('Delete this invite? The link stops working.', {
            title: 'Delete Invite',
            confirmText: 'Delete',
            destructive: true
        }).then(function (ok) {
            if (!ok) return false;
            return Shared.apiRequest('Invites/' + id, 'DELETE')
                .then(function () {
                    Shared.setStatus('inviteStatus', 'Invite deleted.', false);
                    loadAll();
                    return true;
                })
                .catch(function (err) {
                    showError('inviteStatus', err, 'Could not delete the invite.');
                    return false;
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
        _urlSaveBar = createSaveBar({
            fill: true,
            savedText: 'Invite settings saved.',
            onSave: saveBaseUrl
        });
        Shared.getEl('inviteUrlSaveBar').appendChild(_urlSaveBar.element);
        var access = Shared.getEl('manageAccessContent');
        if (access) {
            access.addEventListener('input', function () { _urlSaveBar.setDirty(true); });
            access.addEventListener('change', function () { _urlSaveBar.setDirty(true); });
        }
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

    Page = createPage(view, 1, { bind: bind, load: loadAll, onHide: destroyInviteList });
    Shared = Page.shared;
}
