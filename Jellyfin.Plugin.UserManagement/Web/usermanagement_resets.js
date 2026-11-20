// usermanagement_resets.js
// The Resets tab, which turns reset code extraction on and off and lists pending reset codes by user.

import { createPage, createRecordList, createCopyField, t } from './configurationpage?name=usermanagement_shared.js';

// Resets page
// Builds the Resets tab. Each code sits masked in a Copy Field until revealed.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
export default function (view) {
    'use strict';

    var Shared = null;
    var _list = null;
    var SOON_MS = 5 * 60 * 1000;

    // stateOf
    // Says whether a reset is active, expiring in the next five minutes, or expired. A restart can leave an
    // expired file behind, so expired codes are still listed.
    //
    // Inputs
    // r [object]: One reset from the API.
    // now [Date]: The time to compare against.
    //
    // Output
    // [string]: active, expiring, or expired.
    function stateOf(r, now) {
        var due = r.ExpirationDate ? new Date(r.ExpirationDate) : null;
        if (r.Expired || (due && due <= now)) return 'expired';
        if (due && (due - now) <= SOON_MS) return 'expiring';
        return 'active';
    }

    // tagOf
    // Builds the Record List tag of a state.
    //
    // Inputs
    // state [string]: active, expiring, or expired.
    //
    // Output
    // [object]: The tag, with its label, icon, and color.
    function tagOf(state) {
        if (state === 'expired') return { label: t.tr('StatusExpired'), icon: 'timer_off', color: 'red' };
        if (state === 'expiring') return { label: t.tr('StatusExpiring'), icon: 'timer', color: 'orange' };
        return { label: t.tr('StatusActive'), icon: 'schedule', color: 'green' };
    }

    // toItem
    // Builds one Record List row, with the user, the expiration, a state tag, and the code in a Copy Field.
    //
    // Inputs
    // r [object]: One reset from the API.
    // i [number]: Its index in the loaded list.
    // now [Date]: The time to compare against.
    //
    // Output
    // [object]: The Record List item.
    function toItem(r, i, now) {
        var state = stateOf(r, now);
        var due = r.ExpirationDate ? new Date(r.ExpirationDate) : null;
        var name = r.UserName || t.tr('UnknownUser');
        return {
            id: name + '|' + (r.ExpirationDate || i),
            title: name,
            secondary: due ? (state === 'expired' ? t.format('ExpiredOnDate', due.toLocaleString()) : t.format('ExpiresOnDate', due.toLocaleString())) : null,
            tags: [tagOf(state)],
            state: state,
            aside: createCopyField({
                value: r.Pin,
                secret: true,
                allowExpose: true,
                inline: true,
                size: 'small',
                label: t.format('ResetCodeFor', name),
                copyLabel: t.tr('ButtonCopyCode'),
                copiedText: t.tr('ToastCodeCopied')
            })
        };
    }

    // ensureList
    // Draws the Record List once.
    function ensureList() {
        if (_list) return;
        _list = createRecordList({ label: t.tr('HeaderPendingResets'), emptyText: t.tr('EmptyPendingResets') });
        Shared.getEl('resetList').appendChild(_list.element);
    }

    // destroyList
    // Tears down the Record List and its Copy Fields when the page hides.
    function destroyList() {
        if (!_list) return;
        _list.destroy();
        if (_list.element.parentNode) _list.element.parentNode.removeChild(_list.element);
        _list = null;
    }

    // renderResets
    // Draws the count cards and one row per reset, active first, then expired.
    //
    // Inputs
    // resets [Array]: The resets from the API.
    // enabled [boolean]: Whether reset code extraction is on.
    function renderResets(resets, enabled) {
        ensureList();
        Shared.setVisible('resetDisabled', !enabled);
        Shared.setVisible('resetList', enabled);
        Shared.setVisible('resetCards', enabled);

        var now = new Date();
        var items = enabled ? resets.map(function (r, i) { return toItem(r, i, now); }) : [];
        var rank = { active: 0, expiring: 0, expired: 1 };
        items.sort(function (a, b) { return rank[a.state] - rank[b.state]; });
        var counts = { active: 0, expiring: 0, expired: 0 };
        items.forEach(function (it) { counts[it.state]++; });

        Shared.renderCards('resetCards', [
            { count: counts.active, label: t.tr('StatusActive'), color: 'green' },
            { count: counts.expiring, label: t.tr('StatusExpiring'), color: 'orange' },
            { count: counts.expired, label: t.tr('StatusExpired'), color: 'red' }
        ]);
        _list.setItems(items);
    }

    // load
    // Reads the setting and the pending resets, then draws them.
    function load() {
        Promise.all([
            Shared.getConfig(),
            Shared.apiRequest('Resets', 'GET')
        ]).then(function (results) {
            var enabled = !!(results[0] && results[0].EnableResetCodeExtraction);
            Shared.getEl('chkEnableResets').checked = enabled;
            renderResets((results[1] && results[1].Resets) || [], enabled);
        }).catch(function () {
            Shared.setStatus('resetStatus', t.tr('ErrorLoadResets'), true);
        });
    }

    // setExtractionEnabled
    // Saves whether reset code extraction is on, then reloads the list.
    //
    // Inputs
    // enabled [boolean]: The new setting.
    function setExtractionEnabled(enabled) {
        Shared.getConfig().then(function (cfg) {
            cfg.EnableResetCodeExtraction = enabled;
            return Shared.saveConfig(cfg);
        }).then(function () {
            Shared.setStatus('resetStatus', enabled ? t.tr('ToastResetsEnabled') : t.tr('ToastResetsDisabled'), false);
            load();
        }).catch(function () {
            Shared.setStatus('resetStatus', t.tr('ErrorSaveSetting'), true);
            load();
        });
    }

    // bind
    // Wires the extraction checkbox once.
    function bind() {
        Shared.getEl('chkEnableResets').addEventListener('change', function () {
            setExtractionEnabled(!!this.checked);
        });
    }

    Shared = createPage(view, 2, { bind: bind, load: load, onHide: destroyList }).shared;
}
