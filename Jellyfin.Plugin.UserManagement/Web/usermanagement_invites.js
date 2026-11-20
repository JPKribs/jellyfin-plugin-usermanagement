// usermanagement_invites.js
// The Invites tab, which creates invite links, sets the invite Server URL and rate limit, and lists every invite.

import {
    createPage,
    createKeyValueEditor,
    createQrCode,
    createList,
    createDefinitionList,
    openEditorDialog,
    confirmDialog,
    createSaveBar,
    saveBarOptions,
    withLanguage,
    t
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

    // statusOf
    // Picks an invite's status for its badge and the count cards. state is the Base badge state that colors it,
    // or gray for Used up, which BADGE_STATES has no state for.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [object]: { cls, text, state }, where cls is Active, Locked, UsedUp, or Expired.
    function statusOf(inv) {
        if (inviteExpired(inv)) return { cls: 'Expired', text: t.tr('StatusExpired'), state: 'Expired' };
        if (!inv.Enabled) return { cls: 'Locked', text: t.tr('StatusLocked'), state: 'Locked' };
        if (inv.MaxUses > 0 && inv.UsedCount >= inv.MaxUses) return { cls: 'UsedUp', text: t.tr('StatusUsedUp'), state: 'gray' };
        // The server refuses signups into an expired group, so the link is dead until the group's
        // expiration moves even though the invite itself is still enabled.
        if (groupExpired(inviteGroup(inv))) return { cls: 'Expired', text: t.tr('StatusGroupExpired'), state: 'Expired' };
        return { cls: 'Active', text: t.tr('StatusActive'), state: 'Active' };
    }

    // statusBadge
    // Draws an invite's status as a Base badge.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [HTMLElement]: The badge.
    function statusBadge(inv) {
        var st = statusOf(inv);
        var holder = document.createElement('span');
        holder.innerHTML = Shared.statusBadge(st.text, st.state);
        return holder.firstElementChild || holder;
    }

    // usesText
    // Says how many times an invite was used, out of its limit when it has one.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [string]: Such as "4 of 10 uses" or "4 uses, no limit".
    function usesText(inv) {
        var used = inv.UsedCount || 0;
        if (inv.MaxUses > 0) return inv.MaxUses === 1 ? t.format('UsesOfOne', used) : t.format('UsesOfLimit', used, inv.MaxUses);
        return used === 1 ? t.tr('UsesOneNoLimit') : t.format('UsesNoLimit', used);
    }

    // expiryLine
    // Names an invite's expiration date in the reader's locale. The stored date is a UTC day, so it is shown in UTC.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [object]: { label, value }, such as Expires and Jun 12, 2026, Expired and the date once it passed, or Expires and Never.
    function expiryLine(inv) {
        var day = expiryDate(inv);
        if (!day) return { label: t.tr('LabelExpires'), value: t.tr('Never') };
        var date = new Date(day + 'T00:00:00Z');
        var text = isNaN(date.getTime()) ? day : date.toLocaleDateString(undefined, { dateStyle: 'medium', timeZone: 'UTC' });
        return { label: inviteExpired(inv) ? t.tr('StatusExpired') : t.tr('LabelExpires'), value: text };
    }

    // detailItems
    // Lists an invite's group, uses, expiration, and PIN for a Definition List, in the same order on every card.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [Array]: The Definition List items.
    function detailItems(inv) {
        var expiry = expiryLine(inv);
        return [
            { label: t.tr('LabelGroup'), value: groupText(inv) || null },
            { label: t.tr('LabelUses'), value: usesText(inv) },
            { label: expiry.label, value: expiry.value },
            { label: t.tr('LabelPin'), value: inv.HasPin ? t.tr('PinSet') : null }
        ];
    }

    // linkQr
    // Builds an invite's QR Code: inline on a card, its link in a Copy Field with a button that opens the code, or full in
    // the editor, the whole code drawn beside its link and buttons.
    //
    // Inputs
    // inv [object]: The invite.
    // layout [string]: 'inline' or 'full'.
    //
    // Output
    // [object]: The QR Code handle.
    function linkQr(inv, layout) {
        return createQrCode({
            layout: layout,
            value: inviteUrl(inv.Token),
            label: t.format('InviteLinkOf', inv.Label || t.tr('UntitledInvite')),
            fileName: 'invite-qr-code.png',
            showText: t.tr('ButtonShowQrCode'),
            copyImageText: t.tr('ButtonCopyImage'),
            downloadText: t.tr('Download'),
            copiedText: t.tr('Copied'),
            copyFailedText: t.tr('ToastCopyFailed'),
            tooLongText: t.tr('ErrorQrTooLong')
        });
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
        if (inv.UseDefaultGroup) return _defaultGroupId ? t.format('DefaultGroupNamed', groupName(_defaultGroupId)) : t.tr('DefaultGroup');
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
                pinHelp.textContent = t.format('PinHelpAttempts', attempts);
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
            keyLabel: t.tr('LabelTitle'),
            valueLabel: t.tr('LabelUrl'),
            keyPlaceholder: t.tr('ResourceTitlePlaceholder'),
            valuePlaceholder: 'https://requests.example.com',
            addText: t.tr('ButtonAddResource'),
            removeLabel: t.tr('ButtonRemove'),
            requiredText: t.tr('ErrorEnterTitle'),
            duplicateKeys: true,
            validate: function (key, value) {
                if (!key && !value) return '';
                if (!/^https?:\/\/.+/i.test((value || '').trim())) return t.tr('ErrorEnterFullUrl');
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
            var defName = t.format('DefaultGroupNamed', def.Name || t.tr('UnnamedGroup'));
            defOpt.textContent = groupExpired(def) ? t.format('GroupExpiredSuffix', defName) : defName;
            sel.appendChild(defOpt);
        }

        _groups.forEach(function (g) {
            if (blocksInvites(g)) return;
            var opt = document.createElement('option');
            opt.value = g.Id;
            var name = g.Name || t.tr('UnnamedGroup');
            if (g.Id === _defaultGroupId) name = t.format('GroupDefaultSuffix', name);
            opt.textContent = groupExpired(g) ? t.format('GroupExpiredSuffix', name) : name;
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
            { count: active, label: t.tr('StatusActive'), color: 'green' },
            { count: locked, label: t.tr('StatusLocked'), color: 'purple' },
            { count: consumed, label: t.tr('StatusConsumed'), color: 'gray' },
            { count: expired, label: t.tr('StatusExpired'), color: 'orange' }
        ]);
    }

    // toCard
    // Takes what one invite card shows, so the List redraws a card only when something on it changed.
    //
    // Inputs
    // inv [object]: The invite.
    //
    // Output
    // [object]: { id, inv, url, status, details } for renderCard.
    function toCard(inv) {
        return {
            id: String(inv.Id),
            inv: inv,
            url: inviteUrl(inv.Token),
            status: statusOf(inv),
            details: detailItems(inv)
        };
    }

    // renderCard
    // Draws one invite as a Base panel, its name and status badge in the header, then its details and its inline QR Code.
    // A click on the card opens the invite, except on the QR Code, whose field and buttons act on their own.
    //
    // Inputs
    // card [object]: The card from toCard.
    //
    // Output
    // [object]: { element, destroy }.
    function renderCard(card) {
        var inv = card.inv;
        var el = document.createElement('div');
        el.className = 'base-panel openable um-invite';
        el.tabIndex = 0;
        el.innerHTML = '<div class="base-panel-header um-invite-head"><span class="um-invite-title"></span><span class="um-invite-status"></span></div>'
            + '<div class="base-panel-body um-invite-body"></div>';
        el.querySelector('.um-invite-title').textContent = inv.Label || t.tr('UntitledInvite');
        el.querySelector('.um-invite-status').appendChild(statusBadge(inv));
        var details = createDefinitionList({ items: card.details, emptyText: t.tr('None') });
        var qr = linkQr(inv, 'inline');
        var body = el.querySelector('.um-invite-body');
        body.appendChild(details.element);
        body.appendChild(qr.element);
        el.addEventListener('click', function (e) {
            if (e.target.closest && e.target.closest('.base-qr-code')) return;
            openInvite(inv.Id);
        });
        el.addEventListener('keydown', function (e) {
            if (e.target !== el || (e.key !== 'Enter' && e.key !== ' ')) return;
            e.preventDefault();
            openInvite(inv.Id);
        });
        return { element: el, destroy: function () { details.destroy(); qr.destroy(); } };
    }

    // ensureInviteList
    // Draws the List of invite cards once.
    function ensureInviteList() {
        if (_inviteList) return;
        _inviteList = createList({
            layout: 'plain',
            wrap: false,
            key: 'id',
            label: t.tr('HeaderInvites'),
            emptyText: t.tr('EmptyInvites'),
            render: renderCard
        });
        Shared.getEl('inviteList').appendChild(_inviteList.element);
    }

    // destroyInviteList
    // Tears down the List when the page hides.
    function destroyInviteList() {
        if (!_inviteList) return;
        _inviteList.destroy();
        if (_inviteList.element.parentNode) _inviteList.element.parentNode.removeChild(_inviteList.element);
        _inviteList = null;
    }

    // renderInvites
    // Draws the count cards and one card per invite.
    function renderInvites() {
        renderInviteCards();
        ensureInviteList();
        _inviteList.setItems(_invites.map(toCard));
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
    // Opens an invite in an editor dialog with its link and QR Code, its details, its expiration, and whether it accepts signups.
    // Save writes what changed, Delete asks first, and Duplicate copies it into the Create Invite form.
    //
    // Inputs
    // id [string]: The invite.
    function openInvite(id) {
        var inv = findInvite(id);
        if (!inv) return;
        var body = document.createElement('div');
        body.innerHTML = '<div class="inputContainer"><label class="inputLabel" for="inviteLinkField" data-str="LabelInviteLink"></label><div data-mount="link"></div></div>'
            + '<div data-mount="details"></div>'
            + '<div class="inputContainer"><label class="inputLabel" for="inviteExpiryField" data-str="LabelInviteExpiration"></label>'
            + '<input is="emby-input" type="date" id="inviteExpiryField" />'
            + '<div class="fieldDescription" data-str="InviteExpiryEditHelp"></div></div>'
            + '<div class="checkboxContainer checkboxContainer-withDescription"><label class="emby-checkbox-label">'
            + '<input type="checkbox" is="emby-checkbox" id="inviteEnabledField" /><span class="checkboxLabel" data-str="LabelAcceptSignups"></span></label>'
            + '<div class="fieldDescription checkboxFieldDescription" data-str="AcceptSignupsHelp"></div></div>'
            + '<div><span class="base-status-text" id="inviteDialogStatus" role="status"></span></div>';
        t.apply(body);

        var link = linkQr(inv, 'full');
        var linkInput = link.element.querySelector('input');
        if (linkInput) linkInput.id = 'inviteLinkField';
        body.querySelector('[data-mount="link"]').appendChild(link.element);
        var details = createDefinitionList({
            items: [
                { label: t.tr('LabelStatus'), value: statusBadge(inv) },
                { label: t.tr('LabelGroup'), value: groupText(inv) || null },
                { label: t.tr('LabelUses'), value: usesText(inv) },
                { label: t.tr('LabelPin'), value: inv.HasPin ? t.tr('PinSet') : null }
            ],
            emptyText: t.tr('None')
        });
        body.querySelector('[data-mount="details"]').appendChild(details.element);

        var expiryInput = body.querySelector('#inviteExpiryField');
        var enabledInput = body.querySelector('#inviteEnabledField');
        expiryInput.value = expiryDate(inv);
        enabledInput.checked = !!inv.Enabled;

        openEditorDialog({
            title: inv.Label || t.tr('UntitledInvite'),
            body: body,
            className: 'pluginConfigurationPage',
            size: 'medium',
            page: Page,
            doneText: t.tr('Save'),
            removeText: t.tr('Delete'),
            cancelText: t.tr('ButtonCancel'),
            serialize: function () { return expiryInput.value + '|' + enabledInput.checked; },
            onDone: function () { return saveInvite(inv, expiryInput.value, enabledInput.checked); },
            onRemove: function (close) { deleteInvite(inv.Id).then(function (ok) { if (ok) close(); }); },
            actions: [{
                text: t.tr('ButtonDuplicate'),
                onClick: function (handle) {
                    handle.markClean();
                    handle.close();
                    duplicateInvite(inv.Id);
                }
            }],
            onClose: function () { link.destroy(); details.destroy(); }
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
                Shared.setStatus('inviteDialogStatus', t.tr('ErrorInviteExpiredEnable'), true);
                return Promise.resolve(false);
            }
            var group = inviteGroup(inv);
            if (group && blocksInvites(group)) {
                Shared.setStatus('inviteDialogStatus', t.tr('ErrorInviteGroupBlocksEnable'), true);
                return Promise.resolve(false);
            }
        }

        var chain = Promise.resolve();
        if (expiryChanged) {
            chain = chain.then(function () {
                return Shared.apiRequest(withLanguage('Invites/' + inv.Id + '/Expiry'), 'POST', { ExpiresAt: dateStr ? dateStr + 'T00:00:00' : null });
            });
        }
        if (enabledChanged) {
            chain = chain.then(function () {
                return Shared.apiRequest(withLanguage('Invites/' + inv.Id + '/Enabled'), 'POST', { Enabled: enabled });
            });
        }
        return chain.then(function () {
            if (expiryChanged || enabledChanged) Shared.setStatus('inviteStatus', t.tr('ToastInviteSaved'), false);
            loadAll();
            return true;
        }).catch(function (err) {
            Shared.errorText(err, t.tr('ErrorSaveInvite')).then(function (text) {
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

        Shared.getEl('txtLabel').value = inv.Label ? t.format('InviteCopyName', inv.Label) : '';
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
        Shared.setStatus('inviteStatus', inv.HasPin ? t.tr('ToastInviteCopiedPin') : t.tr('ToastInviteCopied'), false);
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
            Shared.setStatus('inviteStatus', _groups.length ? t.tr('ErrorSelectGroup') : t.tr('ErrorCreateGroupFirst'), true);
            return;
        }
        if (setExpiry && !expVal) {
            Shared.setStatus('inviteStatus', t.tr('ErrorSelectExpiration'), true);
            return;
        }
        if (setMaxUses && (isNaN(maxUses) || maxUses < 1)) {
            Shared.setStatus('inviteStatus', t.tr('ErrorMaxUses'), true);
            return;
        }

        var pin = (Shared.getEl('txtPin').value || '').trim();
        if (pin && !/^\d{6}$/.test(pin)) {
            Shared.setStatus('inviteStatus', t.tr('ErrorPinFormat'), true);
            return;
        }

        if (!_resourceEditor.isValid()) {
            Shared.setStatus('inviteStatus', t.tr('ErrorResourceInvalid'), true);
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
        Shared.apiRequest(withLanguage('Invites'), 'POST', payload)
            .then(function () {
                Shared.setStatus('inviteStatus', expiredGroup ? t.tr('ToastInviteCreatedGroupExpired') : t.tr('ToastInviteCreated'), expiredGroup);
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
                showError('inviteStatus', err, t.tr('ErrorCreateInvite'));
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
            throw new Error(t.tr('ErrorSaveInviteSettings'));
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
        return confirmDialog(t.tr('ConfirmDeleteInvite'), {
            title: t.tr('HeaderDeleteInvite'),
            confirmText: t.tr('Delete'),
            cancelText: t.tr('ButtonCancel'),
            destructive: true
        }).then(function (ok) {
            if (!ok) return false;
            return Shared.apiRequest(withLanguage('Invites/' + id), 'DELETE')
                .then(function () {
                    Shared.setStatus('inviteStatus', t.tr('ToastInviteDeleted'), false);
                    loadAll();
                    return true;
                })
                .catch(function (err) {
                    showError('inviteStatus', err, t.tr('ErrorDeleteInvite'));
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
        _urlSaveBar = createSaveBar(saveBarOptions({
            fill: true,
            savedText: t.tr('ToastInviteSettingsSaved'),
            onSave: saveBaseUrl
        }));
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
