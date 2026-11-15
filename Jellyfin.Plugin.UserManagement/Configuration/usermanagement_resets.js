// usermanagement_resets.js
// The Resets tab, which turns reset code extraction on and off and lists pending reset codes by user.

import { createPage } from './configurationpage?name=usermanagement_shared.js';

// Resets page
// Builds the Resets tab. Each code stays masked until revealed.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
export default function (view) {
    'use strict';

    var Shared = null;
    var _resets = [];

    // mask
    // The text shown in place of a hidden code.
    //
    // Output
    // [string]: Six dots.
    function mask() { return '••••••'; }

    // rowHtml
    // Builds one reset row. The date turns orange in the last five minutes and red once expired.
    // Expired entries are listed apart, since a restart can leave an expired file behind.
    //
    // Inputs
    // r [object]: One reset from the API.
    // i [number]: Its index in the loaded list.
    //
    // Output
    // [string]: The row's HTML.
    function rowHtml(r, i) {
        var esc = Shared.escapeHtml;
        var due = r.ExpirationDate ? new Date(r.ExpirationDate) : null;
        var now = new Date();
        var expired = !!r.Expired || (due && due <= now);
        var soon = !expired && due && (due - now) <= 5 * 60 * 1000;
        var tone = expired ? ' um-text-red' : (soon ? ' um-text-orange' : '');
        var sub = (expired ? 'Expired ' : 'Expires ') + (due ? due.toLocaleString() : '');
        return '<div class="base-table-row um-row">'
            + '<div class="base-table-item-info um-row-info">'
            + '<div class="base-table-item-title">' + (esc(r.UserName) || 'Unknown user') + '</div>'
            + '<div class="base-table-item-sub' + tone + '">' + esc(sub) + '</div>'
            + '</div>'
            + '<code class="um-reset-code" data-index="' + i + '">' + mask() + '</code>'
            + '<button type="button" class="base-row-btn um-reveal" data-index="' + i + '" title="Show" aria-label="Show"><span class="material-icons" aria-hidden="true">visibility</span></button>'
            + '<button type="button" class="base-row-btn um-copy-code" data-index="' + i + '" title="Copy" aria-label="Copy"><span class="material-icons" aria-hidden="true">content_copy</span></button>'
            + '</div>';
    }

    // renderResets
    // Draws the count cards and the active and expired lists.
    //
    // Inputs
    // enabled [boolean]: Whether reset code extraction is on.
    function renderResets(enabled) {
        Shared.setVisible('resetDisabled', !enabled);
        Shared.setVisible('resetEmpty', enabled && _resets.length === 0);
        Shared.setVisible('resetCards', enabled);

        var now = new Date();
        var activeHtml = '';
        var expiredHtml = '';
        var counts = { active: 0, expiring: 0, expired: 0 };
        if (enabled) {
            _resets.forEach(function (r, i) {
                var due = r.ExpirationDate ? new Date(r.ExpirationDate) : null;
                var expired = !!r.Expired || (due && due <= now);
                if (expired) {
                    counts.expired++;
                    expiredHtml += rowHtml(r, i);
                } else {
                    if (due && (due - now) <= 5 * 60 * 1000) counts.expiring++;
                    else counts.active++;
                    activeHtml += rowHtml(r, i);
                }
            });
        }

        Shared.renderCards('resetCards', [
            { count: counts.active, label: 'Active', color: 'green' },
            { count: counts.expiring, label: 'Expiring', color: 'orange' },
            { count: counts.expired, label: 'Expired', color: 'red' }
        ]);
        Shared.getEl('activeList').innerHTML = activeHtml;
        Shared.getEl('expiredList').innerHTML = expiredHtml;
        Shared.setVisible('activeGroup', activeHtml.length > 0);
        Shared.setVisible('expiredGroup', expiredHtml.length > 0);
    }

    // toggleReveal
    // Shows or hides one code.
    //
    // Inputs
    // index [number]: The reset's index in the loaded list.
    function toggleReveal(index) {
        var code = view.querySelector('.um-reset-code[data-index="' + index + '"]');
        var btn = view.querySelector('.um-reveal[data-index="' + index + '"] .material-icons');
        if (!code || !_resets[index]) return;
        var hidden = code.textContent === mask();
        code.textContent = hidden ? _resets[index].Pin : mask();
        if (btn) btn.textContent = hidden ? 'visibility_off' : 'visibility';
        var holder = btn && btn.parentNode;
        if (holder) { holder.title = hidden ? 'Hide' : 'Show'; holder.setAttribute('aria-label', holder.title); }
    }

    // copyCode
    // Copies one code and reports it.
    //
    // Inputs
    // index [number]: The reset's index in the loaded list.
    function copyCode(index) {
        if (!_resets[index]) return;
        Shared.copyToClipboard(_resets[index].Pin).then(function () {
            Shared.setStatus('resetStatus', 'Code copied.', false);
        });
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
            _resets = (results[1] && results[1].Resets) || [];
            renderResets(enabled);
        }).catch(function () {
            Shared.setStatus('resetStatus', 'Could not load the resets.', true);
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
            Shared.setStatus('resetStatus', enabled ? 'Reset code extraction enabled.' : 'Reset code extraction disabled.', false);
            load();
        }).catch(function () {
            Shared.setStatus('resetStatus', 'Could not save the setting.', true);
            load();
        });
    }

    // bind
    // Wires the extraction checkbox and the row buttons once.
    function bind() {
        Shared.getEl('chkEnableResets').addEventListener('change', function () {
            setExtractionEnabled(!!this.checked);
        });
        Shared.getEl('resetGroups').addEventListener('click', function (e) {
            var reveal = e.target.closest('.um-reveal');
            if (reveal) { toggleReveal(parseInt(reveal.getAttribute('data-index'), 10)); return; }
            var copy = e.target.closest('.um-copy-code');
            if (copy) { copyCode(parseInt(copy.getAttribute('data-index'), 10)); }
        });
    }

    Shared = createPage(view, 2, { bind: bind, load: load }).shared;
}
