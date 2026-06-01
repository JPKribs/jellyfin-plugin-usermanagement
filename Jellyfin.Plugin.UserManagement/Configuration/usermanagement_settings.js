export default function (view) {
    'use strict';

    var getTabs;
    var Shared = null;
    var _sharedPromise = import('/web/configurationpage?name=usermanagement_shared.js').then(function (mod) {
        getTabs = mod.getTabs;
        Shared = mod.createShared(view);
    });

    function loadAll() {
        _sharedPromise.then(function () {
            Shared.getConfig().then(function (config) {
                Shared.getEl('txtInviteBaseUrl').value = config.InviteBaseUrl || '';
                populateGroups(config);
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

    function save() {
        Shared.getConfig().then(function (config) {
            config.DefaultGroupId = Shared.getEl('selDefaultGroup').value || null;
            config.InviteBaseUrl = (Shared.getEl('txtInviteBaseUrl').value || '').trim().replace(/\/+$/, '');

            Shared.saveConfig(config).then(function () {
                Shared.setStatus('settingsStatus', 'Settings saved.', false);
                loadAll();
            }).catch(function () {
                Shared.setStatus('settingsStatus', 'Failed to save settings.', true);
            });
        });
    }

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
    });
}
