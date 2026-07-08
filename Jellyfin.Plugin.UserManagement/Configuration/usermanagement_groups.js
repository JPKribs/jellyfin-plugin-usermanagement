export default function (view) {
    'use strict';

    var getTabs, generateGuid, createUserMultiSelector;
    var Shared = null;
    var memberSel = null;
    var memberNotes = {};
    var _sharedPromise = import('/web/configurationpage?name=usermanagement_shared.js').then(function (mod) {
        getTabs = mod.getTabs;
        generateGuid = mod.generateGuid;
        createUserMultiSelector = mod.createUserMultiSelector;
        Shared = mod.createShared(view);
    });

    var pluginId = '670167bd-e7f8-4549-98e2-5ab2e11bc89f';
    var fullConfig = null;
    var currentGroupId = null;
    var allUsers = [];
    var allLibraries = [];
    var allDevices = [];
    var parentalRatings = [];
    var allCultures = [];
    var allCastReceivers = [];
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
            { key: 'EnableMediaConversion', label: 'Allow media conversion', type: 'bool' },
            { key: 'IsDisabled', label: 'Disable this user', type: 'bool',
                desc: 'The server will not allow any connections from this user.' },
            { key: 'IsHidden', label: 'Hide this user from login screens', type: 'bool' },
            { key: 'LoginAttemptsBeforeLockout', label: 'Failed login tries before user is locked out', type: 'int', min: -1,
                desc: 'A value of zero means inheriting the default of three tries for normal users and five for administrators. Setting this to -1 will disable the feature.' },
            { key: 'MaxActiveSessions', label: 'Maximum number of simultaneous user sessions', type: 'int',
                desc: 'A value of 0 will disable the feature.' }
        ] }
    ];

    var SUBTITLE_MODES = [
        ['Default', 'Default', 'Subtitles are loaded based on the default and forced flags in the embedded metadata. Language preferences are considered when multiple options are available.'],
        ['Smart', 'Smart', 'Subtitles matching the language preference will be loaded when the audio is in a foreign language.'],
        ['OnlyForced', 'Only Forced', 'Only subtitles marked as forced will be loaded.'],
        ['Always', 'Always Play', 'Subtitles matching the language preference will be loaded regardless of the audio language.'],
        ['None', 'None', 'Subtitles will not be loaded by default. They can still be turned on manually during playback.']
    ];

    // Display and playback preferences applied to the Jellyfin user configuration, mirroring the
    // fields on a user's Subtitles / Playback / Display / Home preference pages.
    var CONFIG = [
        { title: 'Subtitles', perms: [
            { key: 'SubtitleMode', label: 'Subtitle mode', type: 'submode' },
            { key: 'SubtitleLanguagePreference', label: 'Preferred subtitle language', type: 'culture' },
            { key: 'RememberSubtitleSelections', label: 'Set subtitle track based on previous item', type: 'bool',
                desc: 'Try to set the subtitle track to the closest match to the last video.' }
        ] },
        { title: 'Playback', perms: [
            { key: 'AudioLanguagePreference', label: 'Preferred audio language', type: 'culture' },
            { key: 'PlayDefaultAudioTrack', label: 'Play default audio track regardless of language', type: 'bool', def: true },
            { key: 'RememberAudioSelections', label: 'Set audio track based on previous item', type: 'bool',
                desc: 'Try to set the audio track to the closest match to the last video.' },
            { key: 'EnableNextEpisodeAutoPlay', label: 'Play next episode automatically', type: 'bool' },
            { key: 'CastReceiverId', label: 'Google Cast Version', type: 'castreceiver' }
        ] },
        { title: 'Display', perms: [
            { key: 'DisplayMissingEpisodes', label: 'Display missing episodes within seasons', type: 'bool',
                desc: 'This must also be enabled for TV libraries in the server configuration.' }
        ] },
        { title: 'Home', perms: [
            { key: 'HidePlayedInLatest', label: "Hide watched content from 'Recently Added Media'", type: 'bool', def: true },
            { key: 'OrderedViews', label: 'Media library order', type: 'order',
                desc: 'Set the order libraries appear on the home screen.' },
            { key: 'MyMediaExcludes', label: 'Display on home screen', type: 'libgrid', invert: true,
                desc: 'Choose which libraries appear in the My Media row.' },
            { key: 'LatestItemsExcludes', label: "Display in home screen sections such as 'Recently Added Media' and 'Continue Watching'", type: 'libgrid', invert: true },
            { key: 'GroupedFolders', label: "Automatically group content from the following folders into views such as 'Movies', 'Music' and 'TV'", type: 'libgrid', invert: false,
                desc: 'Folders that are unchecked will be displayed by themselves in their own view.' }
        ] }
    ];

    function fetchCultures() {
        try {
            return ApiClient.getCultures().then(function (r) { return r || []; }).catch(function () { return []; });
        } catch (e) { return Promise.resolve([]); }
    }

    var DEFAULT_CAST_RECEIVERS = [
        { Id: 'F007D354', Name: 'Stable' },
        { Id: '6F511C87', Name: 'Unstable' }
    ];

    function fetchCastReceivers() {
        try {
            return ApiClient.getServerConfiguration()
                .then(function (c) {
                    var list = c && c.CastReceiverApplications;
                    return (list && list.length) ? list : DEFAULT_CAST_RECEIVERS;
                })
                .catch(function () { return DEFAULT_CAST_RECEIVERS; });
        } catch (e) { return Promise.resolve(DEFAULT_CAST_RECEIVERS); }
    }

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
            fetchRatings(),
            fetchCultures(),
            fetchCastReceivers()
        ]).then(function (results) {
            fullConfig = results[0];
            allUsers = results[1] || [];
            allLibraries = results[2] || [];
            allDevices = results[3] || [];
            parentalRatings = results[4] || [];
            allCultures = results[5] || [];
            allCastReceivers = results[6] || [];
            if (!fullConfig.Groups) fullConfig.Groups = [];

            renderSections();
            ensureMemberSelector();
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

    function renderSectionGroup(sections, container, prefix, store) {
        var esc = Shared.escapeHtml;
        var html = '';

        sections.forEach(function (section, si) {
            var id = prefix + si;
            html += '<div class="jpk-collapsible-section">'
                + '<button type="button" class="jpk-collapsible-header collapsed" aria-expanded="false" data-target="' + id + '">'
                + '<span class="jpk-collapsible-title">' + esc(section.title) + '</span>'
                + '<span class="jpk-collapsible-icon">&#9660;</span></button>'
                + '<div id="' + id + '" class="jpk-collapsible-content collapsed">';

            section.perms.forEach(function (p) {
                html += renderPermRow(p, store);
            });

            html += '</div></div>';
        });

        container.innerHTML = html;
    }

    function renderSections() {
        renderSectionGroup(PERMISSIONS, view.querySelector('#permissionSections'), 'permSection', 'perm');
        renderSectionGroup(CONFIG, view.querySelector('#configSections'), 'configSection', 'config');
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

    function cultureOptions() {
        var esc = Shared.escapeHtml;
        var opts = '<option value="">Any Language</option>';
        allCultures.forEach(function (c) {
            var code = c.ThreeLetterISOLanguageName || c.threeLetterISOLanguageName || '';
            var name = c.DisplayName || c.displayName || code;
            if (code) opts += '<option value="' + esc(code) + '">' + esc(name) + '</option>';
        });
        return opts;
    }

    function orderList(itemClass) {
        var esc = Shared.escapeHtml;
        return '<div class="um-order-list ' + itemClass + '">'
            + allLibraries.map(function (l) {
                return '<div class="um-order-row" data-id="' + esc(l.ItemId) + '">'
                    + '<span class="um-order-name">' + esc(l.Name) + '</span>'
                    + '<span class="um-order-actions">'
                    + '<button type="button" class="um-order-btn um-order-up" title="Move up"><span class="material-icons" aria-hidden="true">keyboard_arrow_up</span></button>'
                    + '<button type="button" class="um-order-btn um-order-down" title="Move down"><span class="material-icons" aria-hidden="true">keyboard_arrow_down</span></button>'
                    + '</span></div>';
            }).join('')
            + '</div>';
    }

    function renderPermRow(p, store) {
        var esc = Shared.escapeHtml;
        var control = '';
        store = store || 'perm';

        if (p.type === 'bool') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-value" />'
                + '<span class="checkboxLabel">Enabled</span></label>';
        } else if (p.type === 'int') {
            control = '<input is="emby-input" type="number" min="' + (p.min != null ? p.min : 0) + '" class="perm-value jpk-edit-input" />';
        } else if (p.type === 'bitrate') {
            control = '<input is="emby-input" type="number" min="0" step="0.1" class="perm-value jpk-edit-input" />';
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
                + '<input is="emby-input" type="text" class="perm-tag-input jpk-edit-input" placeholder="Tag" />'
                + '<button is="emby-button" type="button" class="raised jpk-button-small perm-tag-add"><span>Add</span></button>'
                + '</div></div>';
        } else if (p.type === 'schedule') {
            control = '<div class="um-schedules"><div class="perm-sched-list"></div>'
                + '<button is="emby-button" type="button" class="raised jpk-button-small perm-sched-add" style="margin-top:6px;"><span>Add schedule</span></button>'
                + '</div>';
        } else if (p.type === 'submode') {
            var mopts = SUBTITLE_MODES.map(function (m) {
                return '<option value="' + esc(m[0]) + '">' + esc(m[1]) + '</option>';
            }).join('');
            control = '<select is="emby-select" class="perm-value emby-select-withcolor">' + mopts + '</select>'
                + '<div class="fieldDescription perm-submode-help"></div>';
        } else if (p.type === 'culture') {
            control = '<select is="emby-select" class="perm-value emby-select-withcolor">' + cultureOptions() + '</select>';
        } else if (p.type === 'castreceiver') {
            var copts = allCastReceivers.map(function (r) {
                return '<option value="' + esc(r.Id || r.id) + '">' + esc(r.Name || r.name || r.Id) + '</option>';
            }).join('');
            control = '<select is="emby-select" class="perm-value emby-select-withcolor">' + copts + '</select>';
        } else if (p.type === 'order') {
            control = orderList('perm-order');
        } else if (p.type === 'libgrid') {
            control = checkGrid('perm-lib-grid', 'perm-lib', allLibraries,
                function (l) { return l.ItemId; }, function (l) { return l.Name; });
        }

        var desc = p.desc ? '<div class="fieldDescription">' + esc(p.desc) + '</div>' : '';
        var labelHtml = p.label ? '<div class="um-perm-label">' + esc(p.label) + '</div>' : '';
        var invertAttr = (p.type === 'libgrid') ? ' data-invert="' + (p.invert ? '1' : '0') + '"' : '';
        var defAttr = p.def ? ' data-default="1"' : '';

        return '<div class="um-perm-row inherited" data-store="' + esc(store) + '" data-perm="' + esc(p.key) + '" data-type="' + esc(p.type) + '"' + invertAttr + defAttr + '>'
            + '<label class="emby-checkbox-label um-perm-toggle" title="Override this setting for the group">'
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
            + '<button is="emby-button" type="button" class="raised jpk-button-small jpk-button-destructive perm-sched-remove"><span>Remove</span></button>'
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

            // Re-filter the home-screen library lists whenever Library Access changes.
            if (row.getAttribute('data-perm') === 'LibraryAccess') {
                row.addEventListener('change', applyAccessFilter);
            }

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
            } else if (type === 'submode') {
                var sel = row.querySelector('.perm-value');
                sel.addEventListener('change', function () { updateSubmodeHelp(row); });
            } else if (type === 'order') {
                row.querySelector('.perm-order').addEventListener('click', function (e) {
                    var btn = e.target.closest('.um-order-up, .um-order-down');
                    if (!btn || btn.disabled) return;
                    var item = btn.closest('.um-order-row');
                    if (btn.classList.contains('um-order-up')) {
                        if (item.previousElementSibling) item.parentNode.insertBefore(item, item.previousElementSibling);
                    } else if (item.nextElementSibling) {
                        item.parentNode.insertBefore(item.nextElementSibling, item);
                    }
                    checkDirty();
                });
            }
        });
    }

    function updateSubmodeHelp(row) {
        var help = row.querySelector('.perm-submode-help');
        if (!help) return;
        var val = row.querySelector('.perm-value').value;
        var mode = SUBTITLE_MODES.find(function (m) { return m[0] === val; });
        help.textContent = mode ? mode[2] : '';
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
        btn.classList.toggle('jpk-button-submit', isDefault);
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

    function loadConfigRow(row, cfg, key, type, manage) {
        manage.checked = cfg['Manage' + key] === true;

        if (type === 'order') {
            var list = row.querySelector('.perm-order');
            // Reset to the default library order first so switching groups never leaves stale ordering.
            allLibraries.forEach(function (l) {
                var el = list.querySelector('.um-order-row[data-id="' + l.ItemId + '"]');
                if (el) list.appendChild(el);
            });
            (cfg.OrderedViews || []).slice().reverse().forEach(function (id) {
                var el = list.querySelector('.um-order-row[data-id="' + id + '"]');
                if (el) list.insertBefore(el, list.firstChild);
            });
        } else if (type === 'libgrid') {
            var invert = row.getAttribute('data-invert') === '1';
            var stored = cfg[key] || [];
            row.querySelectorAll('.perm-lib').forEach(function (cb) {
                var listed = stored.indexOf(cb.value) !== -1;
                cb.checked = invert ? !listed : listed;
            });
        } else if (type === 'submode') {
            row.querySelector('.perm-value').value = cfg.SubtitleMode || 'Default';
            updateSubmodeHelp(row);
        } else if (type === 'culture' || type === 'castreceiver') {
            var sel = row.querySelector('.perm-value');
            var val = cfg[key];
            sel.value = (val == null) ? (sel.options[0] ? sel.options[0].value : '') : val;
        } else {
            var v = row.querySelector('.perm-value');
            var stored2 = cfg[key];
            v.checked = (stored2 === true || stored2 === false)
                ? stored2 === true
                : row.getAttribute('data-default') === '1';
        }
    }

    function collectConfigRow(row, cfg, key, type, managed) {
        cfg['Manage' + key] = managed;

        if (type === 'order') {
            // Only accessible libraries are shown, so only those are persisted in the order.
            var ids = [];
            row.querySelectorAll('.um-order-row:not(.hidden)').forEach(function (el) { ids.push(el.getAttribute('data-id')); });
            cfg.OrderedViews = ids;
        } else if (type === 'libgrid') {
            var invert = row.getAttribute('data-invert') === '1';
            var out = [];
            row.querySelectorAll('.perm-lib').forEach(function (cb) {
                var label = cb.closest('.emby-checkbox-label');
                if (label && label.classList.contains('hidden')) return;
                if (invert ? !cb.checked : cb.checked) out.push(cb.value);
            });
            cfg[key] = out;
        } else if (type === 'submode' || type === 'culture' || type === 'castreceiver') {
            cfg[key] = row.querySelector('.perm-value').value;
        } else {
            cfg[key] = row.querySelector('.perm-value').checked;
        }
    }

    // The set of library ids the group's members can see, read live from the Library Access row.
    // Returns null when every library is accessible (access unmanaged or "all libraries" enabled).
    function accessibleLibraryIds() {
        var row = view.querySelector('.um-perm-row[data-perm="LibraryAccess"]');
        if (!row) return null;
        var managed = row.querySelector('.perm-manage').checked;
        var allFolders = row.querySelector('.perm-allfolders').checked;
        if (!managed || allFolders) return null;
        var set = {};
        row.querySelectorAll('.perm-folder:checked').forEach(function (cb) { set[cb.value] = true; });
        return set;
    }

    // Hides home-screen library rows (order and grids) the group's members cannot access, so an admin
    // cannot order or toggle a library the group hides from them.
    function applyAccessFilter() {
        var access = accessibleLibraryIds();
        var accessible = function (id) { return access === null || access[id] === true; };

        view.querySelectorAll('#configSections .um-perm-row').forEach(function (row) {
            var type = row.getAttribute('data-type');
            if (type === 'order') {
                row.querySelectorAll('.um-order-row').forEach(function (el) {
                    el.classList.toggle('hidden', !accessible(el.getAttribute('data-id')));
                });
            } else if (type === 'libgrid') {
                row.querySelectorAll('.perm-lib').forEach(function (cb) {
                    var label = cb.closest('.emby-checkbox-label');
                    if (label) label.classList.toggle('hidden', !accessible(cb.value));
                });
            }
        });
    }

    function loadCurrentGroup() {
        updateDefaultButton();
        var group = getCurrentGroup();
        if (!group) return;
        var perms = group.Permissions || {};
        var cfg = group.Configuration || {};

        view.querySelectorAll('.um-perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var manage = row.querySelector('.perm-manage');

            if (row.getAttribute('data-store') === 'config') {
                loadConfigRow(row, cfg, key, type, manage);
                updateRowInherited(row);
                return;
            }

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

        applyAccessFilter();
        loadGroupExtras(group);
        if (memberSel) {
            refreshMemberAvailability(group);
        }
        updateMemberSummary();
    }

    function refreshMemberAvailability(group) {
        var notes = {};
        var others = [];
        // A user belongs to one group at a time, so users already in another group are shown
        // disabled here with that group's name; remove them there first to move them.
        fullConfig.Groups.forEach(function (g) {
            if (g.Id === currentGroupId) return;
            (g.MemberIds || []).forEach(function (id) {
                notes[id] = g.Name || 'Another group';
                others.push(id);
            });
        });
        memberNotes = notes;
        memberSel.setValue((group && group.MemberIds) || []);
        memberSel.setDisabled(others);
    }

    function loadGroupExtras(group) {
        var pw = group.Password || {};
        var el = view.querySelector.bind(view);
        if (el('#grpPwEnabled')) el('#grpPwEnabled').checked = pw.Enabled === true;
        if (el('#grpPwLength')) el('#grpPwLength').value = pw.MinLength || 8;
        // Defaults to checked for a group with no saved policy, matching the server-side default.
        if (el('#grpPwNoEmpty')) el('#grpPwNoEmpty').checked = pw.DisallowEmpty !== false;
        if (el('#grpPwChangeMode')) el('#grpPwChangeMode').value = normalizeChangeMode(pw.ChangeMode);
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
            ChangeMode: normalizeChangeMode((el('#grpPwChangeMode') || {}).value),
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

    var CHANGE_MODES = ['Allowed', 'InitialOnly', 'Disallowed'];

    // The generic configuration endpoint may serialize the enum as a number or a string.
    function normalizeChangeMode(value) {
        if (typeof value === 'number') return CHANGE_MODES[value] || 'Allowed';
        return CHANGE_MODES.indexOf(value) >= 0 ? value : 'Allowed';
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
        return '<div class="jpk-card ' + cls + '"><span class="jpk-card-count">' + count
            + '</span><span class="jpk-card-label">' + label + '</span></div>';
    }

    function renderUserCards() {
        var container = view.querySelector('#userCards');
        if (!container) return;
        var staleCutoff = Date.now() - 30 * 86400000;
        var admins = 0, active = 0, inactive = 0, disabled = 0;
        var adminIds = {};
        allUsers.forEach(function (u) {
            var p = u.Policy || {};
            if (p.IsAdministrator) { admins++; adminIds[u.Id] = true; return; }
            if (p.IsDisabled) { disabled++; return; }
            var last = u.LastActivityDate || u.LastLoginDate;
            if (last && new Date(last).getTime() >= staleCutoff) { active++; } else { inactive++; }
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
            card('blue', admins, 'Admins') +
            card('green', active, 'Active') +
            card('yellow', inactive, 'Inactive') +
            card('red', disabled, 'Disabled') +
            card('purple', Object.keys(expiringIds).length, 'Expiring');
    }

    function collectCurrentGroup() {
        var group = getCurrentGroup();
        if (!group) return;
        if (!group.Permissions) group.Permissions = {};
        if (!group.Configuration) group.Configuration = {};
        var perms = group.Permissions;
        var cfg = group.Configuration;

        view.querySelectorAll('.um-perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var managed = row.querySelector('.perm-manage').checked;

            if (row.getAttribute('data-store') === 'config') {
                collectConfigRow(row, cfg, key, type, managed);
                return;
            }

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

    function ensureMemberSelector() {
        var mount = view.querySelector('#memberMount');
        if (!mount || !createUserMultiSelector) return;
        if (memberSel) { memberSel.refresh(); return; }
        memberSel = createUserMultiSelector({
            adminFilter: 'exclude',
            showAvatars: true,
            fetchUsers: function () { return allUsers; },
            emptyMessage: 'No users available.',
            disabledIds: [],
            noteFor: function (u) { return memberNotes[u.id] || ''; },
            onChange: onMembersChanged
        });
        mount.appendChild(memberSel.element);
    }

    function onMembersChanged(ids) {
        var group = getCurrentGroup();
        if (!group) return;
        group.MemberIds = (ids || []).slice();
        // A user belongs to one group at a time; selecting them here removes them from any other group.
        fullConfig.Groups.forEach(function (g) {
            if (g.Id === currentGroupId) return;
            g.MemberIds = (g.MemberIds || []).filter(function (x) { return group.MemberIds.indexOf(x) === -1; });
        });
        updateMemberSummary();
        checkDirty();
    }

    function updateMemberSummary() {
        var group = getCurrentGroup();
        var n = group ? (group.MemberIds || []).length : 0;
        var el = view.querySelector('#memberSummary');
        if (el) el.textContent = n + ' member' + (n !== 1 ? 's' : '') + ' in this group';
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
            var group = { Id: generateGuid(), Name: name, MemberIds: [], Permissions: {}, Configuration: {} };
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
