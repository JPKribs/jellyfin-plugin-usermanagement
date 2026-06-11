export default function (view) {
    'use strict';

    var getTabs;
    var Shared = null;
    var _sharedPromise = import('/web/configurationpage?name=usermanagement_shared.js').then(function (mod) {
        getTabs = mod.getTabs;
        Shared = mod.createShared(view);
    });

    var _resets = [];
    var _bound = false;

    function mask() { return '••••••'; }

    // The date stays its normal color until five minutes before expiry, turns amber inside that
    // window, and turns red once expired. Expired entries are grouped separately. The server deletes
    // a reset file when its code is redeemed, but a restart between creation and redemption can leave
    // an expired file behind, so the grouping keeps those readable without offering any file actions.
    function rowHtml(r, i) {
        var esc = Shared.escapeHtml;
        var due = r.ExpirationDate ? new Date(r.ExpirationDate) : null;
        var now = new Date();
        var expired = !!r.Expired || (due && due <= now);
        var soon = !expired && due && (due - now) <= 5 * 60 * 1000;
        var color = expired ? 'var(--jpk-red-fg)' : (soon ? 'var(--jpk-orange-fg)' : '');
        var sub = (expired ? 'Expired ' : 'Expires ') + (due ? due.toLocaleString() : '');
        return '<div class="jpk-table-row" style="align-items:center; gap:8px;">'
            + '<div class="jpk-table-item-info" style="flex:1;">'
            + '<div class="jpk-table-item-title" style="font-weight:bold;">' + (esc(r.UserName) || 'Unknown user') + '</div>'
            + '<div class="jpk-table-item-sub"' + (color ? ' style="color:' + color + ';"' : '') + '>' + esc(sub) + '</div>'
            + '</div>'
            + '<code class="um-reset-code" data-index="' + i + '" style="letter-spacing:2px; min-width:90px; text-align:center;">' + mask() + '</code>'
            + '<button type="button" class="um-btn um-reveal" data-index="' + i + '" title="Reveal"><span class="material-icons">visibility</span></button>'
            + '<button type="button" class="um-btn um-copy-code" data-index="' + i + '" title="Copy"><span class="material-icons">content_copy</span></button>'
            + '</div>';
    }

    function card(cls, count, label) {
        return '<div class="jpk-card ' + cls + '"><span class="jpk-card-count">' + count
            + '</span><span class="jpk-card-label">' + label + '</span></div>';
    }

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

        Shared.getEl('resetCards').innerHTML =
            card('green', counts.active, 'Active') +
            card('yellow', counts.expiring, 'Expiring') +
            card('red', counts.expired, 'Expired');
        Shared.getEl('activeList').innerHTML = activeHtml;
        Shared.getEl('expiredList').innerHTML = expiredHtml;
        Shared.setVisible('activeGroup', activeHtml.length > 0);
        Shared.setVisible('expiredGroup', expiredHtml.length > 0);
    }

    function toggleReveal(index) {
        var code = view.querySelector('.um-reset-code[data-index="' + index + '"]');
        var btn = view.querySelector('.um-reveal[data-index="' + index + '"] .material-icons');
        if (!code || !_resets[index]) return;
        var hidden = code.textContent === mask();
        code.textContent = hidden ? _resets[index].Pin : mask();
        if (btn) btn.textContent = hidden ? 'visibility_off' : 'visibility';
    }

    function copyCode(index) {
        if (!_resets[index]) return;
        var pin = _resets[index].Pin;
        var done = function () { Shared.setStatus('resetStatus', 'Code copied.', false); };
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(pin).then(done).catch(done);
        } else {
            var tmp = document.createElement('textarea');
            tmp.value = pin;
            view.appendChild(tmp);
            tmp.select();
            try { document.execCommand('copy'); } catch (e) { /* ignore */ }
            view.removeChild(tmp);
            done();
        }
    }

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
            Shared.setStatus('resetStatus', 'Failed to load resets.', true);
        });
    }

    function setExtractionEnabled(enabled) {
        Shared.getConfig().then(function (cfg) {
            cfg.EnableResetCodeExtraction = enabled;
            return Shared.saveConfig(cfg);
        }).then(function () {
            Shared.setStatus('resetStatus', enabled ? 'Reset code extraction enabled.' : 'Reset code extraction disabled.', false);
            load();
        }).catch(function () {
            Shared.setStatus('resetStatus', 'Save failed.', true);
            load();
        });
    }

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

    view.addEventListener('viewshow', function () {
        _sharedPromise.then(function () {
            LibraryMenu.setTabs('usermanagement', 2, getTabs);
            if (!_bound) { bind(); _bound = true; }
            load();
        });
    });
}
