export default function (view) {
    'use strict';

    var getTabs, generateGuid;
    var Shared = null;
    var _sharedPromise = import('/web/configurationpage?name=usermanagement_shared.js').then(function (mod) {
        getTabs = mod.getTabs;
        generateGuid = mod.generateGuid;
        Shared = mod.createShared(view);
    });

    var pluginId = '670167bd-e7f8-4549-98e2-5ab2e11bc89f';
    var fullConfig = null;
    var currentGroupId = null;
    var allUsers = [];
    var allLibraries = [];
    var allDevices = [];
    var parentalRatings = [];
    var _snapshot = null;
    var _dirty = false;

    var UNRATED_ITEMS = [
        ['Book', 'Books'], ['ChannelContent', 'Channels'], ['LiveTvChannel', 'Live TV'],
        ['Movie', 'Movies'], ['Music', 'Music'], ['Trailer', 'Trailers'], ['Series', 'Shows']
    ];

    var DAY_OPTIONS = ['Everyday', 'Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Weekday', 'Weekend'];

    var PERMISSIONS = [
        { title: 'Library Access', perms: [
            { key: 'LibraryAccess', label: '', type: 'library',
                desc: 'Select the libraries to share with this user.' },
            { key: 'ChannelAccess', label: 'Channels', type: 'channels' }
        ] },
        { title: 'Device Access', perms: [
            { key: 'DeviceAccess', label: '', type: 'devices' }
        ] },
        { title: 'Media Deletion', perms: [
            { key: 'EnableContentDeletion', label: '', type: 'deletion' }
        ] },
        { title: 'Media Playback', perms: [
            { key: 'EnableMediaPlayback', label: 'Allow media playback', type: 'bool' },
            { key: 'EnableAudioPlaybackTranscoding', label: 'Allow audio playback that requires transcoding', type: 'bool' },
            { key: 'EnableVideoPlaybackTranscoding', label: 'Allow video playback that requires transcoding', type: 'bool' },
            { key: 'EnablePlaybackRemuxing', label: 'Allow video playback that requires conversion without re-encoding', type: 'bool' },
            { key: 'ForceRemoteSourceTranscoding', label: 'Force transcoding of remote media sources such as Live TV', type: 'bool',
                desc: 'Restricting access to transcoding may cause playback failures in clients due to unsupported media formats.' },
            { key: 'RemoteClientBitrateLimit', label: 'Internet streaming bitrate limit (Mbps)', type: 'bitrate',
                desc: 'An optional per-stream bitrate limit for all out of network devices.' },
            { key: 'SyncPlayAccess', label: 'SyncPlay access', type: 'enum',
                desc: 'The SyncPlay feature enables to sync playback with other devices.',
                options: [
                    ['CreateAndJoinGroups', 'Allow user to create and join groups'],
                    ['JoinGroups', 'Allow user to join groups'],
                    ['None', 'Disabled for this user']
                ] }
        ] },
        { title: 'Live TV', perms: [
            { key: 'EnableLiveTvAccess', label: 'Allow Live TV access', type: 'bool' },
            { key: 'EnableLiveTvManagement', label: 'Allow Live TV recording management', type: 'bool' }
        ] },
        { title: 'Remote Control', perms: [
            { key: 'EnableRemoteControlOfOtherUsers', label: 'Allow remote control of other users', type: 'bool' },
            { key: 'EnableSharedDeviceControl', label: 'Allow remote control of shared devices', type: 'bool',
                desc: 'DLNA devices are considered shared until a user begins controlling them.' }
        ] },
        { title: 'Parental Control', perms: [
            { key: 'MaxParentalRating', label: 'Maximum allowed parental rating', type: 'rating',
                desc: 'Content with a higher rating will be hidden from this user.' },
            { key: 'BlockUnratedItems', label: 'Block items with no or unrecognized rating information', type: 'unrated' },
            { key: 'AllowedTags', label: 'Allow items with tags', type: 'tags',
                desc: 'Only show media with at least one of the specified tags.' },
            { key: 'BlockedTags', label: 'Block items with tags', type: 'tags',
                desc: 'Hide media with at least one of the specified tags.' },
            { key: 'AccessSchedules', label: 'Access Schedule', type: 'schedule',
                desc: 'Create an access schedule to limit access to certain hours.' }
        ] },
        { title: 'Profile', perms: [
            { key: 'EnableRemoteAccess', label: 'Allow remote connections to this server', type: 'bool',
                desc: 'If unchecked, all remote connections will be blocked.' },
            { key: 'EnableCollectionManagement', label: 'Allow this user to manage collections', type: 'bool' },
            { key: 'EnableSubtitleManagement', label: 'Allow this user to edit subtitles', type: 'bool' },
            { key: 'EnableLyricManagement', label: 'Allow this user to edit lyrics', type: 'bool' }
        ] },
        { title: 'Other', perms: [
            { key: 'EnableContentDownloading', label: 'Allow media downloads', type: 'bool',
                desc: 'Users can download media and store it on their devices. Book libraries require this enabled to function properly.' },
            { key: 'IsDisabled', label: 'Disable this user', type: 'bool',
                desc: 'The server will not allow any connections from this user.' },
            { key: 'IsHidden', label: 'Hide this user from login screens', type: 'bool' },
            { key: 'LoginAttemptsBeforeLockout', label: 'Failed login tries before user is locked out', type: 'int', min: -1,
                desc: 'A value of zero means inheriting the default of three tries for normal users and five for administrators. Setting this to -1 will disable the feature.' },
            { key: 'MaxActiveSessions', label: 'Maximum number of simultaneous user sessions', type: 'int',
                desc: 'A value of 0 will disable the feature.' }
        ] }
    ];

    function fetchDevices() {
        try {
            return ApiClient.getJSON(ApiClient.getUrl('Devices'))
                .then(function (r) { return (r && r.Items) || []; })
                .catch(function () { return []; });
        } catch (e) { return Promise.resolve([]); }
    }

    function fetchRatings() {
        try {
            if (ApiClient.getParentalRatings) {
                return ApiClient.getParentalRatings().then(function (r) { return r || []; }).catch(function () { return []; });
            }
        } catch (e) { /* fall through */ }
        return Promise.resolve([]);
    }

    function loadAll() {
        Dashboard.showLoadingMsg();
        Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            ApiClient.getUsers(),
            ApiClient.getVirtualFolders(),
            fetchDevices(),
            fetchRatings()
        ]).then(function (results) {
            fullConfig = results[0];
            allUsers = results[1] || [];
            allLibraries = results[2] || [];
            allDevices = results[3] || [];
            parentalRatings = results[4] || [];
            if (!fullConfig.Groups) fullConfig.Groups = [];

            renderSections();
            populateDropdown();
            snapshot();
            renderUserCards();
            Dashboard.hideLoadingMsg();
        }).catch(function (err) {
            console.error('Failed to load groups', err);
            Dashboard.hideLoadingMsg();
        });
    }

    function ratingScore(r) {
        var rs = r.RatingScore || r.ratingScore;
        if (rs) return rs.Score != null ? rs.Score : rs.score;
        return r.Value != null ? r.Value : r.value;
    }
    function ratingSubScore(r) {
        var rs = r.RatingScore || r.ratingScore;
        if (rs) return rs.SubScore != null ? rs.SubScore : rs.subScore;
        return null;
    }
    function ratingName(r) { return r.Name || r.name || ''; }

    function renderSections() {
        var container = view.querySelector('#permissionSections');
        var esc = Shared.escapeHtml;
        var html = '';

        PERMISSIONS.forEach(function (section, si) {
            var id = 'permSection' + si;
            html += '<div class="collapsibleSection">'
                + '<button type="button" class="collapsibleHeader collapsed" aria-expanded="false" data-target="' + id + '">'
                + '<span class="collapsibleTitle">' + esc(section.title) + '</span>'
                + '<span class="collapsibleIcon">&#9660;</span></button>'
                + '<div id="' + id + '" class="collapsibleContent collapsed">';

            section.perms.forEach(function (p) {
                html += renderPermRow(p);
            });

            html += '</div></div>';
        });

        container.innerHTML = html;

        Shared.initCollapsibles();
        bindPermRows();
    }

    function checkGrid(gridClass, itemClass, items, valueOf, nameOf) {
        var esc = Shared.escapeHtml;
        return '<div class="um-folder-grid ' + gridClass + '">'
            + items.map(function (it) {
                return '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="' + itemClass + '" value="'
                    + esc(valueOf(it)) + '" /><span class="checkboxLabel">' + esc(nameOf(it)) + '</span></label>';
            }).join('')
            + '</div>';
    }

    function libraryGrid(itemClass) {
        return checkGrid('perm-folders', itemClass, allLibraries,
            function (l) { return l.ItemId; }, function (l) { return l.Name; });
    }

    function renderPermRow(p) {
        var esc = Shared.escapeHtml;
        var control = '';

        if (p.type === 'bool') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-value" />'
                + '<span class="checkboxLabel">Enabled</span></label>';
        } else if (p.type === 'int') {
            control = '<input is="emby-input" type="number" min="' + (p.min != null ? p.min : 0) + '" class="perm-value um-edit-input" />';
        } else if (p.type === 'bitrate') {
            control = '<input is="emby-input" type="number" min="0" step="0.1" class="perm-value um-edit-input" />';
        } else if (p.type === 'enum') {
            var opts = (p.options || []).map(function (o) {
                return '<option value="' + esc(o[0]) + '">' + esc(o[1]) + '</option>';
            }).join('');
            control = '<select is="emby-select" class="perm-value emby-select-withcolor">' + opts + '</select>';
        } else if (p.type === 'library') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-allfolders" />'
                + '<span class="checkboxLabel">Enable access to all libraries</span></label>'
                + libraryGrid('perm-folder');
        } else if (p.type === 'deletion') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-alldelete" />'
                + '<span class="checkboxLabel">All libraries</span></label>'
                + checkGrid('perm-delete-folders', 'perm-delete-folder', allLibraries,
                    function (l) { return l.ItemId; }, function (l) { return l.Name; });
        } else if (p.type === 'channels') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-allchannels" />'
                + '<span class="checkboxLabel">Enable access to all channels</span></label>';
        } else if (p.type === 'devices') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-alldevices" />'
                + '<span class="checkboxLabel">Enable access from all devices</span></label>'
                + checkGrid('perm-devices', 'perm-device', allDevices,
                    function (d) { return d.Id; }, function (d) { return d.Name || d.AppName || d.Id; });
        } else if (p.type === 'rating') {
            var ropts = '<option value=""></option>';
            parentalRatings.forEach(function (r, i) {
                ropts += '<option value="' + i + '">' + esc(ratingName(r)) + '</option>';
            });
            control = '<select is="emby-select" class="perm-value emby-select-withcolor">' + ropts + '</select>';
        } else if (p.type === 'unrated') {
            control = '<div class="um-folder-grid perm-unrated-grid">' + UNRATED_ITEMS.map(function (u) {
                return '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-unrated" value="'
                    + esc(u[0]) + '" /><span class="checkboxLabel">' + esc(u[1]) + '</span></label>';
            }).join('') + '</div>';
        } else if (p.type === 'tags') {
            control = '<div class="um-tags"><div class="perm-tag-list um-tag-list"></div>'
                + '<div class="um-tag-add-row">'
                + '<input is="emby-input" type="text" class="perm-tag-input um-edit-input" placeholder="Tag" />'
                + '<button is="emby-button" type="button" class="raised button-small perm-tag-add"><span>Add</span></button>'
                + '</div></div>';
        } else if (p.type === 'schedule') {
            control = '<div class="um-schedules"><div class="perm-sched-list"></div>'
                + '<button is="emby-button" type="button" class="raised button-small perm-sched-add" style="margin-top:6px;"><span>Add schedule</span></button>'
                + '</div>';
        }

        var desc = p.desc ? '<div class="fieldDescription">' + esc(p.desc) + '</div>' : '';
        var labelHtml = p.label ? '<div class="um-perm-label">' + esc(p.label) + '</div>' : '';

        return '<div class="um-perm-row inherited" data-perm="' + esc(p.key) + '" data-type="' + esc(p.type) + '">'
            + '<label class="emby-checkbox-label um-perm-toggle" title="Override this permission for the group">'
            + '<input type="checkbox" is="emby-checkbox" class="perm-manage" />'
            + '<span class="checkboxLabel">Override</span></label>'
            + '<div class="um-perm-main">'
            + labelHtml
            + '<div class="um-perm-control">' + control + '</div>'
            + desc
            + '</div></div>';
    }

    function renderTagChips(row, tags) {
        var esc = Shared.escapeHtml;
        var list = row.querySelector('.perm-tag-list');
        list.innerHTML = (tags || []).map(function (t) {
            return '<span class="um-tag-chip" data-tag="' + esc(t) + '">' + esc(t)
                + ' <button type="button" class="perm-tag-remove" aria-label="Remove">&times;</button></span>';
        }).join('');
    }

    function rowTags(row) {
        var tags = [];
        row.querySelectorAll('.um-tag-chip').forEach(function (c) { tags.push(c.getAttribute('data-tag')); });
        return tags;
    }

    function formatHour(h) {
        h = Number(h) || 0;
        var ampm = h % 24 < 12 ? 'AM' : 'PM';
        var hr = h % 12; if (hr === 0) hr = 12;
        return hr + ':00 ' + ampm;
    }

    function schedRowHtml(s) {
        var esc = Shared.escapeHtml;
        var days = DAY_OPTIONS.map(function (d) {
            return '<option value="' + d + '"' + (s.DayOfWeek === d ? ' selected' : '') + '>' + esc(d) + '</option>';
        }).join('');
        var hrs = function (sel) {
            var o = '';
            for (var h = 0; h <= 24; h++) {
                o += '<option value="' + h + '"' + (Number(sel) === h ? ' selected' : '') + '>' + formatHour(h) + '</option>';
            }
            return o;
        };
        return '<div class="perm-sched-row">'
            + '<select is="emby-select" class="perm-sched-day emby-select-withcolor">' + days + '</select>'
            + '<select is="emby-select" class="perm-sched-start emby-select-withcolor">' + hrs(s.StartHour) + '</select>'
            + '<span class="perm-sched-sep">&ndash;</span>'
            + '<select is="emby-select" class="perm-sched-end emby-select-withcolor">' + hrs(s.EndHour) + '</select>'
            + '<button is="emby-button" type="button" class="raised button-small button-destructive perm-sched-remove"><span>Remove</span></button>'
            + '</div>';
    }

    function renderSchedules(row, schedules) {
        var list = row.querySelector('.perm-sched-list');
        list.innerHTML = (schedules || []).map(schedRowHtml).join('');
    }

    function rowSchedules(row) {
        var out = [];
        row.querySelectorAll('.perm-sched-row').forEach(function (r) {
            out.push({
                DayOfWeek: r.querySelector('.perm-sched-day').value,
                StartHour: parseFloat(r.querySelector('.perm-sched-start').value) || 0,
                EndHour: parseFloat(r.querySelector('.perm-sched-end').value) || 0
            });
        });
        return out;
    }

    function bindPermRows() {
        view.querySelectorAll('.um-perm-row').forEach(function (row) {
            var type = row.getAttribute('data-type');
            row.querySelector('.perm-manage').addEventListener('change', function () { updateRowInherited(row); });

            row.querySelectorAll('.perm-allfolders, .perm-alldelete, .perm-alldevices').forEach(function (toggle) {
                toggle.addEventListener('change', function () { updateFolderVisibility(row); });
            });

            if (type === 'tags') {
                var add = function () {
                    var input = row.querySelector('.perm-tag-input');
                    var v = (input.value || '').trim();
                    if (!v) return;
                    var tags = rowTags(row);
                    if (tags.indexOf(v) === -1) { tags.push(v); renderTagChips(row, tags); }
                    input.value = '';
                    checkDirty();
                };
                row.querySelector('.perm-tag-add').addEventListener('click', add);
                row.querySelector('.perm-tag-input').addEventListener('keydown', function (e) {
                    if (e.key === 'Enter') { e.preventDefault(); add(); }
                });
                row.querySelector('.perm-tag-list').addEventListener('click', function (e) {
                    var btn = e.target.closest('.perm-tag-remove');
                    if (!btn) return;
                    btn.closest('.um-tag-chip').remove();
                    checkDirty();
                });
            } else if (type === 'schedule') {
                row.querySelector('.perm-sched-add').addEventListener('click', function () {
                    var list = row.querySelector('.perm-sched-list');
                    list.insertAdjacentHTML('beforeend', schedRowHtml({ DayOfWeek: 'Everyday', StartHour: 0, EndHour: 24 }));
                    updateRowInherited(row);
                    checkDirty();
                });
                row.querySelector('.perm-sched-list').addEventListener('click', function (e) {
                    var btn = e.target.closest('.perm-sched-remove');
                    if (!btn) return;
                    btn.closest('.perm-sched-row').remove();
                    checkDirty();
                });
            }
        });
    }

    function updateRowInherited(row) {
        var managed = row.querySelector('.perm-manage').checked;
        row.classList.toggle('inherited', !managed);
        row.querySelectorAll('.um-perm-control input, .um-perm-control select, .um-perm-control button')
            .forEach(function (el) { el.disabled = !managed; });
        if (managed) updateFolderVisibility(row);
    }

    function updateFolderVisibility(row) {
        var pairs = [
            ['.perm-allfolders', '.perm-folders'],
            ['.perm-alldelete', '.perm-delete-folders'],
            ['.perm-alldevices', '.perm-devices']
        ];
        pairs.forEach(function (pair) {
            var all = row.querySelector(pair[0]);
            var grid = row.querySelector(pair[1]);
            if (all && grid) {
                grid.style.display = all.checked ? 'none' : 'grid';
            }
        });
    }

    function groupLabel(g) {
        return (g.Name || 'Unnamed group') + (g.Id === fullConfig.DefaultGroupId ? ' (Default)' : '');
    }

    function refreshGroupLabels() {
        var select = view.querySelector('#selectGroup');
        if (!select) return;
        Array.prototype.forEach.call(select.options, function (opt) {
            var g = fullConfig.Groups.find(function (x) { return x.Id === opt.value; });
            if (g) opt.textContent = groupLabel(g);
        });
    }

    function updateDefaultButton() {
        var btn = view.querySelector('#btnDefaultGroup');
        if (!btn) return;
        var isDefault = !!currentGroupId && currentGroupId === fullConfig.DefaultGroupId;
        btn.classList.toggle('button-submit', isDefault);
        var icon = btn.querySelector('.material-icons');
        if (icon) icon.textContent = isDefault ? 'star' : 'star_border';
        btn.setAttribute('title', isDefault ? 'This is the default group — click to unset' : 'Set as default group');
    }

    function toggleDefaultGroup() {
        if (!currentGroupId) return;
        fullConfig.DefaultGroupId = (fullConfig.DefaultGroupId === currentGroupId) ? null : currentGroupId;
        refreshGroupLabels();
        updateDefaultButton();
        checkDirty();
    }

    function populateDropdown() {
        var select = view.querySelector('#selectGroup');
        var previous = currentGroupId;
        select.innerHTML = '';

        var groups = fullConfig.Groups.slice().sort(function (a, b) {
            return (a.Name || '').localeCompare(b.Name || '');
        });

        groups.forEach(function (g) {
            var opt = document.createElement('option');
            opt.value = g.Id;
            opt.textContent = groupLabel(g);
            select.appendChild(opt);
        });

        var hasGroups = groups.length > 0;
        Shared.setVisible('groupEditor', hasGroups);
        Shared.setVisible('emptyState', !hasGroups);
        view.querySelector('#btnRenameGroup').classList.toggle('hidden', !hasGroups);
        view.querySelector('#btnDeleteGroup').classList.toggle('hidden', !hasGroups);
        view.querySelector('#btnDefaultGroup').classList.toggle('hidden', !hasGroups);

        if (!hasGroups) { currentGroupId = null; updateDefaultButton(); return; }

        if (previous && groups.some(function (g) { return g.Id === previous; })) {
            currentGroupId = previous;
        } else {
            currentGroupId = groups[0].Id;
        }
        select.value = currentGroupId;
        loadCurrentGroup();
    }

    function getCurrentGroup() {
        return fullConfig.Groups.find(function (g) { return g.Id === currentGroupId; });
    }

    function snapshot() {
        if (currentGroupId) collectCurrentGroup();
        _snapshot = JSON.stringify(fullConfig);
        _dirty = false;
        updateDirtyIndicator();
    }

    function checkDirty() {
        if (_snapshot === null) return;
        if (currentGroupId) collectCurrentGroup();
        _dirty = JSON.stringify(fullConfig) !== _snapshot;
        updateDirtyIndicator();
    }

    function updateDirtyIndicator() {
        Shared.setVisible('unsavedIndicator', _dirty);
    }

    function persistGroups() {
        return ApiClient.getPluginConfiguration(pluginId).then(function (latest) {
            latest.Groups = fullConfig.Groups;
            latest.DefaultGroupId = fullConfig.DefaultGroupId || null;
            if (latest.DefaultGroupId && !latest.Groups.some(function (g) { return g.Id === latest.DefaultGroupId; })) {
                latest.DefaultGroupId = null;
            }
            return ApiClient.updatePluginConfiguration(pluginId, latest).then(function (result) {
                fullConfig = latest;
                Dashboard.processPluginConfigurationUpdateResult(result);
                return result;
            });
        });
    }

    function loadCurrentGroup() {
        updateDefaultButton();
        var group = getCurrentGroup();
        if (!group) return;
        var perms = group.Permissions || {};

        view.querySelectorAll('.um-perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var manage = row.querySelector('.perm-manage');

            if (type === 'library') {
                manage.checked = perms.ManageLibraryAccess === true;
                row.querySelector('.perm-allfolders').checked = perms.EnableAllFolders !== false;
                var enabled = perms.EnabledFolders || [];
                row.querySelectorAll('.perm-folder').forEach(function (cb) { cb.checked = enabled.indexOf(cb.value) !== -1; });
            } else if (type === 'channels') {
                manage.checked = perms.ManageChannelAccess === true;
                row.querySelector('.perm-allchannels').checked = perms.EnableAllChannels !== false;
            } else if (type === 'deletion') {
                manage.checked = perms.ManageEnableContentDeletion === true;
                row.querySelector('.perm-alldelete').checked = perms.EnableContentDeletion === true;
                var delFolders = perms.EnableContentDeletionFromFolders || [];
                row.querySelectorAll('.perm-delete-folder').forEach(function (cb) { cb.checked = delFolders.indexOf(cb.value) !== -1; });
            } else if (type === 'devices') {
                manage.checked = perms.ManageDeviceAccess === true;
                row.querySelector('.perm-alldevices').checked = perms.EnableAllDevices !== false;
                var devs = perms.EnabledDevices || [];
                row.querySelectorAll('.perm-device').forEach(function (cb) { cb.checked = devs.indexOf(cb.value) !== -1; });
            } else if (type === 'rating') {
                manage.checked = perms.ManageMaxParentalRating === true;
                var sel = row.querySelector('.perm-value');
                sel.value = '';
                if (perms.MaxParentalRating != null) {
                    for (var i = 0; i < parentalRatings.length; i++) {
                        var sub = ratingSubScore(parentalRatings[i]);
                        if (ratingScore(parentalRatings[i]) === perms.MaxParentalRating
                            && (sub == null ? null : sub) === (perms.MaxParentalSubRating == null ? null : perms.MaxParentalSubRating)) {
                            sel.value = String(i); break;
                        }
                    }
                }
            } else if (type === 'unrated') {
                manage.checked = perms.ManageBlockUnratedItems === true;
                var blocked = perms.BlockUnratedItems || [];
                row.querySelectorAll('.perm-unrated').forEach(function (cb) { cb.checked = blocked.indexOf(cb.value) !== -1; });
            } else if (type === 'tags') {
                manage.checked = perms['Manage' + key] === true;
                renderTagChips(row, perms[key] || []);
            } else if (type === 'schedule') {
                manage.checked = perms.ManageAccessSchedules === true;
                renderSchedules(row, perms.AccessSchedules || []);
            } else if (type === 'bitrate') {
                manage.checked = perms.ManageRemoteClientBitrateLimit === true;
                row.querySelector('.perm-value').value = (perms.RemoteClientBitrateLimit || 0) / 1000000 || '';
            } else {
                manage.checked = perms['Manage' + key] === true;
                var val = row.querySelector('.perm-value');
                if (type === 'bool') {
                    val.checked = perms[key] === true;
                } else if (type === 'int') {
                    val.value = perms[key] || 0;
                } else {
                    val.value = perms[key] || (val.options[0] && val.options[0].value);
                }
            }

            updateRowInherited(row);
        });

        loadGroupExtras(group);
        renderMembers();
    }

    function loadGroupExtras(group) {
        var pw = group.Password || {};
        var el = view.querySelector.bind(view);
        if (el('#grpPwEnabled')) el('#grpPwEnabled').checked = pw.Enabled === true;
        if (el('#grpPwLength')) el('#grpPwLength').value = pw.MinLength || 8;
        if (el('#grpPwNoEmpty')) el('#grpPwNoEmpty').checked = pw.DisallowEmpty === true;
        if (el('#grpPwUpper')) el('#grpPwUpper').checked = pw.RequireUppercase === true;
        if (el('#grpPwNumber')) el('#grpPwNumber').checked = pw.RequireNumber === true;
        if (el('#grpPwSymbol')) el('#grpPwSymbol').checked = pw.RequireSymbol === true;
        updatePwEnabledState();

        if (el('#grpExpiryEnabled')) el('#grpExpiryEnabled').checked = !!group.ExpiresOn;
        if (el('#grpExpiryDate')) el('#grpExpiryDate').value = group.ExpiresOn ? String(group.ExpiresOn).slice(0, 10) : '';
        if (el('#grpExpiryAction')) el('#grpExpiryAction').value = group.ExpiryAction || 'Disable';
        updateExpiryEnabledState();

        if (el('#grpInactiveEnabled')) el('#grpInactiveEnabled').checked = group.DisableInactiveUsers === true;
        if (el('#grpInactiveDays')) el('#grpInactiveDays').value = group.InactiveDays || 30;
        updateInactiveEnabledState();
    }

    function collectGroupExtras(group) {
        var el = view.querySelector.bind(view);
        var len = parseInt((el('#grpPwLength') || {}).value, 10);
        group.Password = {
            Enabled: !!(el('#grpPwEnabled') || {}).checked,
            MinLength: isNaN(len) || len < 1 ? 8 : len,
            DisallowEmpty: !!(el('#grpPwNoEmpty') || {}).checked,
            RequireUppercase: !!(el('#grpPwUpper') || {}).checked,
            RequireNumber: !!(el('#grpPwNumber') || {}).checked,
            RequireSymbol: !!(el('#grpPwSymbol') || {}).checked
        };

        var expiryOn = !!(el('#grpExpiryEnabled') || {}).checked;
        var dateVal = (el('#grpExpiryDate') || {}).value || '';
        group.ExpiresOn = (expiryOn && dateVal) ? dateVal + 'T00:00:00' : null;
        group.ExpiryAction = (el('#grpExpiryAction') || {}).value || 'Disable';

        group.DisableInactiveUsers = !!(el('#grpInactiveEnabled') || {}).checked;
        var inactiveDays = parseInt((el('#grpInactiveDays') || {}).value, 10);
        group.InactiveDays = isNaN(inactiveDays) || inactiveDays < 1 ? 30 : inactiveDays;
    }

    function updatePwEnabledState() {
        Shared.setVisible('pwFields', !!(view.querySelector('#grpPwEnabled') || {}).checked);
    }

    function updateExpiryEnabledState() {
        Shared.setVisible('grpExpiryFields', !!(view.querySelector('#grpExpiryEnabled') || {}).checked);
    }

    function updateInactiveEnabledState() {
        Shared.setVisible('inactiveFields', !!(view.querySelector('#grpInactiveEnabled') || {}).checked);
    }

    function card(cls, count, label) {
        return '<div class="um-card ' + cls + '"><span class="um-card-count">' + count
            + '</span><span class="um-card-label">' + label + '</span></div>';
    }

    function renderUserCards() {
        var container = view.querySelector('#userCards');
        if (!container) return;
        var admins = 0, active = 0, inactive = 0;
        var adminIds = {};
        allUsers.forEach(function (u) {
            var p = u.Policy || {};
            if (p.IsAdministrator) { admins++; adminIds[u.Id] = true; return; }
            if (p.IsDisabled) { inactive++; } else { active++; }
        });

        var today = new Date(); today.setHours(0, 0, 0, 0);
        var soon = new Date(today); soon.setDate(soon.getDate() + 7);
        var expiringIds = {};
        (fullConfig.Groups || []).forEach(function (g) {
            if (!g.ExpiresOn) return;
            var d = new Date(g.ExpiresOn);
            if (isNaN(d.getTime())) return;
            d.setHours(0, 0, 0, 0);
            if (d >= today && d <= soon) {
                (g.MemberIds || []).forEach(function (id) { if (!adminIds[id]) expiringIds[id] = true; });
            }
        });

        container.innerHTML =
            card('admin', admins, 'Admins') +
            card('active', active, 'Active') +
            card('inactive', inactive, 'Inactive') +
            card('expiring', Object.keys(expiringIds).length, 'Expiring Soon');
    }

    function collectCurrentGroup() {
        var group = getCurrentGroup();
        if (!group) return;
        if (!group.Permissions) group.Permissions = {};
        var perms = group.Permissions;

        view.querySelectorAll('.um-perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var managed = row.querySelector('.perm-manage').checked;

            if (type === 'library') {
                perms.ManageLibraryAccess = managed;
                perms.EnableAllFolders = row.querySelector('.perm-allfolders').checked;
                var folders = [];
                row.querySelectorAll('.perm-folder:checked').forEach(function (cb) { folders.push(cb.value); });
                perms.EnabledFolders = folders;
            } else if (type === 'channels') {
                perms.ManageChannelAccess = managed;
                perms.EnableAllChannels = row.querySelector('.perm-allchannels').checked;
            } else if (type === 'deletion') {
                perms.ManageEnableContentDeletion = managed;
                perms.EnableContentDeletion = row.querySelector('.perm-alldelete').checked;
                var delFolders = [];
                row.querySelectorAll('.perm-delete-folder:checked').forEach(function (cb) { delFolders.push(cb.value); });
                perms.EnableContentDeletionFromFolders = delFolders;
            } else if (type === 'devices') {
                perms.ManageDeviceAccess = managed;
                perms.EnableAllDevices = row.querySelector('.perm-alldevices').checked;
                var devs = [];
                row.querySelectorAll('.perm-device:checked').forEach(function (cb) { devs.push(cb.value); });
                perms.EnabledDevices = devs;
            } else if (type === 'rating') {
                perms.ManageMaxParentalRating = managed;
                var sel = row.querySelector('.perm-value');
                if (sel.value === '' || parentalRatings[sel.value] === undefined) {
                    perms.MaxParentalRating = null;
                    perms.MaxParentalSubRating = null;
                } else {
                    var r = parentalRatings[parseInt(sel.value, 10)];
                    perms.MaxParentalRating = ratingScore(r);
                    var sub = ratingSubScore(r);
                    perms.MaxParentalSubRating = sub == null ? null : sub;
                }
            } else if (type === 'unrated') {
                perms.ManageBlockUnratedItems = managed;
                var items = [];
                row.querySelectorAll('.perm-unrated:checked').forEach(function (cb) { items.push(cb.value); });
                perms.BlockUnratedItems = items;
            } else if (type === 'tags') {
                perms['Manage' + key] = managed;
                perms[key] = rowTags(row);
            } else if (type === 'schedule') {
                perms.ManageAccessSchedules = managed;
                perms.AccessSchedules = rowSchedules(row);
            } else if (type === 'bitrate') {
                perms.ManageRemoteClientBitrateLimit = managed;
                var mbps = parseFloat(row.querySelector('.perm-value').value);
                perms.RemoteClientBitrateLimit = isNaN(mbps) || mbps < 0 ? 0 : Math.round(mbps * 1000000);
            } else {
                perms['Manage' + key] = managed;
                var val = row.querySelector('.perm-value');
                if (type === 'bool') {
                    perms[key] = val.checked;
                } else if (type === 'int') {
                    perms[key] = parseInt(val.value, 10) || 0;
                } else {
                    perms[key] = val.value;
                }
            }
        });

        collectGroupExtras(group);
    }

    function otherGroupOf(userId) {
        var found = null;
        fullConfig.Groups.forEach(function (g) {
            if (g.Id !== currentGroupId && (g.MemberIds || []).indexOf(userId) !== -1) {
                found = g.Name || 'another group';
            }
        });
        return found;
    }

    function renderMembers() {
        var group = getCurrentGroup();
        if (!group) return;
        var esc = Shared.escapeHtml;
        var term = ((view.querySelector('#memberSearch') || {}).value || '').toLowerCase();
        var list = view.querySelector('#memberList');
        var members = group.MemberIds || [];

        var rows = '';
        allUsers.forEach(function (u) {
            if (term && (u.Name || '').toLowerCase().indexOf(term) === -1) return;

            var inThis = members.indexOf(u.Id) !== -1;
            var isAdmin = !!(u.Policy && u.Policy.IsAdministrator);
            var other = inThis ? null : otherGroupOf(u.Id);

            var disabled = isAdmin || (!inThis && !!other);
            var note = '';
            if (isAdmin) {
                note = '<span class="um-check-note admin">Admin</span>';
            } else if (other) {
                note = '<span class="um-check-note">' + esc(other) + '</span>';
            }

            rows += '<label class="um-check-row' + (disabled ? ' disabled' : '') + '">'
                + '<input type="checkbox" class="um-member-toggle" data-id="' + esc(u.Id) + '"'
                + (inThis ? ' checked' : '') + (disabled ? ' disabled' : '') + ' />'
                + '<span class="um-check-name">' + esc(u.Name) + '</span>'
                + note
                + '</label>';
        });

        list.innerHTML = rows || '<div class="um-item-sub" style="padding:8px;">No users match.</div>';

        list.querySelectorAll('.um-member-toggle').forEach(function (cb) {
            cb.addEventListener('change', function () {
                toggleMember(cb.getAttribute('data-id'), cb.checked);
            });
        });

        updateMemberSummary();
    }

    function updateMemberSummary() {
        var group = getCurrentGroup();
        var n = group ? (group.MemberIds || []).length : 0;
        var el = view.querySelector('#memberSummary');
        if (el) el.textContent = n + ' member' + (n !== 1 ? 's' : '') + ' in this group';
    }

    function toggleMember(userId, checked) {
        var group = getCurrentGroup();
        if (!group) return;
        if (checked) {
            fullConfig.Groups.forEach(function (g) {
                g.MemberIds = (g.MemberIds || []).filter(function (x) { return x !== userId; });
            });
            group.MemberIds.push(userId);
        } else {
            group.MemberIds = (group.MemberIds || []).filter(function (x) { return x !== userId; });
        }
        renderMembers();
        checkDirty();
    }

    function filterMembers() {
        renderMembers();
    }

    function showInputModal(title, value, callback) {
        var modal = view.querySelector('#inputModal');
        var field = view.querySelector('#inputModalField');
        view.querySelector('#inputModalTitle').textContent = title;
        field.value = value || '';
        modal.style.display = 'flex';
        setTimeout(function () { field.focus(); field.select(); }, 50);

        var confirm = view.querySelector('#btnConfirmInputModal');
        var cancel = view.querySelector('#btnCancelInputModal');
        var close = view.querySelector('#btnCloseInputModal');

        function cleanup() {
            confirm.removeEventListener('click', onOk);
            cancel.removeEventListener('click', onCancel);
            close.removeEventListener('click', onCancel);
            field.removeEventListener('keydown', onKey);
            modal.style.display = 'none';
        }
        function onOk() { var v = field.value; cleanup(); callback(v); }
        function onCancel() { cleanup(); callback(null); }
        function onKey(e) {
            if (e.key === 'Enter') { e.preventDefault(); onOk(); }
            else if (e.key === 'Escape') { e.preventDefault(); onCancel(); }
        }

        confirm.addEventListener('click', onOk);
        cancel.addEventListener('click', onCancel);
        close.addEventListener('click', onCancel);
        field.addEventListener('keydown', onKey);
    }

    function nameExists(name, exceptId) {
        return fullConfig.Groups.some(function (g) {
            return g.Id !== exceptId && (g.Name || '').toLowerCase() === name.toLowerCase();
        });
    }

    function newGroup() {
        showInputModal('New Group', '', function (name) {
            if (name === null) return;
            name = name.trim();
            if (!name) { Dashboard.alert('Group name is required.'); return; }
            if (nameExists(name, null)) { Dashboard.alert('A group with this name already exists.'); return; }

            if (currentGroupId) collectCurrentGroup();
            var group = { Id: generateGuid(), Name: name, MemberIds: [], Permissions: {} };
            fullConfig.Groups.push(group);
            currentGroupId = group.Id;
            populateDropdown();
            checkDirty();
        });
    }

    function renameGroup() {
        var group = getCurrentGroup();
        if (!group) return;
        showInputModal('Rename Group', group.Name, function (name) {
            if (name === null) return;
            name = name.trim();
            if (!name) return;
            if (nameExists(name, group.Id)) { Dashboard.alert('A group with this name already exists.'); return; }
            group.Name = name;
            populateDropdown();
            checkDirty();
        });
    }

    function deleteGroup() {
        var group = getCurrentGroup();
        if (!group) return;
        Dashboard.confirm('Delete group "' + group.Name + '"? Members will be unassigned.', 'Delete Group', function (ok) {
            if (!ok) return;
            if (fullConfig.DefaultGroupId === group.Id) fullConfig.DefaultGroupId = null;
            fullConfig.Groups = fullConfig.Groups.filter(function (g) { return g.Id !== group.Id; });
            currentGroupId = null;
            populateDropdown();
            checkDirty();
        });
    }

    function save() {
        if (currentGroupId) collectCurrentGroup();

        var cur = getCurrentGroup();
        if (cur && cur.ExpiresOn && cur.ExpiryAction === 'Delete'
            && !confirm('Group "' + (cur.Name || '') + '" is set to DELETE its members on ' + String(cur.ExpiresOn).slice(0, 10)
                + '. This permanently removes those accounts and cannot be undone. Save anyway?')) {
            return;
        }

        Dashboard.showLoadingMsg();
        persistGroups().then(function () {
            return Shared.apiRequest('Apply', 'POST');
        }).then(function () {
            Dashboard.hideLoadingMsg();
            snapshot();
            renderUserCards();
            Shared.setStatus('groupStatus', 'Saved and applied to members.', false);
        }).catch(function (err) {
            console.error('Save failed', err);
            Dashboard.hideLoadingMsg();
            Shared.setStatus('groupStatus', 'Save failed.', true);
        });
    }

    function onBeforeUnload(e) {
        if (_dirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    }

    view.addEventListener('viewshow', function () {
        _sharedPromise.then(function () {
            LibraryMenu.setTabs('usermanagement', 0, getTabs);
            window.addEventListener('beforeunload', onBeforeUnload);
            loadAll();
        });
    });

    view.addEventListener('viewbeforehide', function (e) {
        window.removeEventListener('beforeunload', onBeforeUnload);
        if (_dirty && !confirm('You have unsaved changes. Leave without saving?')) {
            e.preventDefault();
            LibraryMenu.setTabs('usermanagement', 0, getTabs);
        }
    });

    _sharedPromise.then(function () {
        view.querySelector('#selectGroup').addEventListener('change', function () {
            collectCurrentGroup();
            currentGroupId = this.value;
            loadCurrentGroup();
        });
        view.querySelector('#btnNewGroup').addEventListener('click', newGroup);
        view.querySelector('#btnRenameGroup').addEventListener('click', renameGroup);
        view.querySelector('#btnDeleteGroup').addEventListener('click', deleteGroup);
        view.querySelector('#btnSaveGroups').addEventListener('click', save);
        view.querySelector('#btnDefaultGroup').addEventListener('click', toggleDefaultGroup);

        view.querySelector('#memberSearch').addEventListener('input', filterMembers);

        var pwEnabled = view.querySelector('#grpPwEnabled');
        if (pwEnabled) pwEnabled.addEventListener('change', updatePwEnabledState);

        var expiryEnabled = view.querySelector('#grpExpiryEnabled');
        if (expiryEnabled) expiryEnabled.addEventListener('change', updateExpiryEnabledState);

        var inactiveEnabled = view.querySelector('#grpInactiveEnabled');
        if (inactiveEnabled) inactiveEnabled.addEventListener('change', updateInactiveEnabledState);

        var form = view.querySelector('#UserManagementGroupsForm');
        if (form) {
            form.addEventListener('change', checkDirty);
            form.addEventListener('input', checkDirty);
        }
    });
}
