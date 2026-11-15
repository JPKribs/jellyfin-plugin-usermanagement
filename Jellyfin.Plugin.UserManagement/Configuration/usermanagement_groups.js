// usermanagement_groups.js
// The Groups tab, which edits each group's members, settings, preferences, and lifecycle rules.

import {
    createPage,
    generateGuid,
    createUserMultiSelector,
    createAccessSchedule,
    createTagInput,
    createOrderedList,
    createInheritField,
    createRepeater,
    createChipSelect,
    confirmDialog,
    promptDialog
} from './configurationpage?name=usermanagement_shared.js';

// Groups page
// Builds the Groups tab. Each setting is an Inherit Field with an Override checkbox, and only overridden settings reach members.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
export default function (view) {
    'use strict';

    var Shared = null;
    var memberSel = null;
    var memberNotes = {};
    var tracker = null;
    var rowHandles = {};
    var inheritHandles = {};
    var sessionRules = null;
    var sessionClients = [];
    var orderState = [];
    var loading = false;

    var pluginId = '670167bd-e7f8-4549-98e2-5ab2e11bc89f';
    var fullConfig = null;
    var currentGroupId = null;
    var allUsers = [];
    var allLibraries = [];
    var allDevices = [];
    var parentalRatings = [];
    var allCultures = [];
    var allCastReceivers = [];
    var allViews = [];
    var allGroupable = [];

    var UNRATED_ITEMS = [
        ['Book', 'Books'], ['ChannelContent', 'Channels'], ['LiveTvChannel', 'Live TV'],
        ['Movie', 'Movies'], ['Music', 'Music'], ['Trailer', 'Trailers'], ['Series', 'Shows']
    ];

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
            { key: 'EnableLyricManagement', label: 'Allow this user to edit lyrics', type: 'bool' },
            { key: 'EnableUserPreferenceAccess', label: 'Allow this user to change their own preferences', type: 'bool',
                desc: 'If unchecked, display and playback preferences can only be changed by an administrator or by this group.' }
        ] },
        { title: 'Other', perms: [
            { key: 'EnableContentDownloading', label: 'Allow media downloads', type: 'bool',
                desc: 'Users can download media and store it on their devices. Book libraries require this enabled to function properly.' },
            { key: 'EnableMediaConversion', label: 'Allow media conversion', type: 'bool' },
            { key: 'EnableSyncTranscoding', label: 'Allow sync transcoding', type: 'bool',
                desc: 'Allows the server to transcode media that a client downloads for offline use.' },
            { key: 'EnablePublicSharing', label: 'Allow public sharing', type: 'bool',
                desc: 'Allows this user to create public share links for media.' },
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

    // Home screen section options for the web client, in the order and wording Jellyfin uses.
    // The value keys map (case-insensitively) onto the server's HomeSectionType.
    var HOME_SECTIONS = [
        ['smalllibrarytiles', 'My Media'],
        ['librarybuttons', 'My Media (small)'],
        ['activerecordings', 'Active Recordings'],
        ['resume', 'Continue Watching'],
        ['resumeaudio', 'Continue Listening'],
        ['resumebook', 'Continue Reading'],
        ['latestmedia', 'Recently Added Media'],
        ['nextup', 'Next Up'],
        ['livetv', 'Live TV'],
        ['none', 'None']
    ];

    // Jellyfin's default home layout, used when a group has not set an explicit order yet.
    var DEFAULT_HOME_SECTIONS = ['smalllibrarytiles', 'resume', 'resumeaudio', 'resumebook', 'livetv', 'nextup', 'latestmedia', 'none', 'none', 'none'];
    var HOME_SECTION_COUNT = 10;

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
                desc: 'This must also be enabled for TV libraries in the server configuration.' },
            { key: 'DisplayCollectionsView', label: "Display a 'Collections' view", type: 'bool',
                desc: 'Shows a Collections entry alongside the libraries on the home screen.' }
        ] },
        { title: 'Home', perms: [
            { key: 'HomeSections', label: 'Customize how the Home Sections are presented to this group.', type: 'homesections',
                desc: 'Set the order of the home screen sections for the web client. This only affects applicable clients.' },
            { key: 'HidePlayedInLatest', label: "Hide watched content from 'Recently Added Media'", type: 'bool', def: true },
            { key: 'OrderedViews', label: 'Media library order', type: 'order',
                desc: 'Set the order libraries appear on the home screen.' },
            { key: 'MyMediaExcludes', label: 'Display on home screen', type: 'libgrid', invert: true,
                desc: 'Choose which libraries appear in the My Media row.' },
            { key: 'LatestItemsExcludes', label: "Display in home screen sections such as 'Recently Added Media' and 'Continue Watching'", type: 'libgrid', invert: true },
            { key: 'GroupedFolders', label: "Automatically group content from the following folders into views such as 'Movies', 'Music' and 'TV'", type: 'libgrid', invert: false, source: 'groupable',
                desc: 'Folders that are unchecked will be displayed by themselves in their own view.' }
        ] }
    ];

    // fetchCultures
    // Reads the server's languages for the language selects.
    //
    // Output
    // [Promise]: The cultures, or an empty list on failure.
    function fetchCultures() {
        try {
            return ApiClient.getCultures().then(function (r) { return r || []; }).catch(function () { return []; });
        } catch (e) { return Promise.resolve([]); }
    }

    var DEFAULT_CAST_RECEIVERS = [
        { Id: 'F007D354', Name: 'Stable' },
        { Id: '6F511C87', Name: 'Unstable' }
    ];

    // fetchCastReceivers
    // Reads the Google Cast receivers, falling back to Jellyfin's two defaults.
    //
    // Output
    // [Promise]: The receivers.
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

    // fetchDevices
    // Reads the devices known to the server.
    //
    // Output
    // [Promise]: The devices, or an empty list on failure.
    function fetchDevices() {
        try {
            return ApiClient.getJSON(ApiClient.getUrl('Devices'))
                .then(function (r) { return (r && r.Items) || []; })
                .catch(function () { return []; });
        } catch (e) { return Promise.resolve([]); }
    }

    // fetchUserViews
    // Reads the home screen views, as Jellyfin's own library order lists them. They include derived views
    // such as Live TV and Collections, which getVirtualFolders leaves out.
    //
    // Output
    // [Promise]: The views, or an empty list on failure.
    function fetchUserViews() {
        try {
            var uid = ApiClient.getCurrentUserId();
            return ApiClient.getJSON(ApiClient.getUrl('Users/' + uid + '/Views', { includeHidden: true }))
                .then(function (r) { return (r && r.Items) ? r.Items : []; })
                .catch(function () { return []; });
        } catch (e) { return Promise.resolve([]); }
    }

    // fetchGroupingOptions
    // Reads the folders Jellyfin can group into type views such as Movies and TV, which is a subset of the libraries.
    //
    // Output
    // [Promise]: The folders, or an empty list on failure.
    function fetchGroupingOptions() {
        try {
            var uid = ApiClient.getCurrentUserId();
            return ApiClient.getJSON(ApiClient.getUrl('Users/' + uid + '/GroupingOptions'))
                .then(function (r) { return (r && r.Items) ? r.Items : (Array.isArray(r) ? r : []); })
                .catch(function () { return []; });
        } catch (e) { return Promise.resolve([]); }
    }

    // fetchRatings
    // Reads the parental ratings.
    //
    // Output
    // [Promise]: The ratings, or an empty list on failure.
    function fetchRatings() {
        try {
            if (ApiClient.getParentalRatings) {
                return ApiClient.getParentalRatings().then(function (r) { return r || []; }).catch(function () { return []; });
            }
        } catch (e) {
            return Promise.resolve([]);
        }
        return Promise.resolve([]);
    }

    // loadAll
    // Reads the config and everything the rows list, then draws the page.
    function loadAll() {
        Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            ApiClient.getUsers(),
            ApiClient.getVirtualFolders(),
            fetchDevices(),
            fetchRatings(),
            fetchCultures(),
            fetchCastReceivers(),
            fetchUserViews(),
            fetchGroupingOptions()
        ]).then(function (results) {
            fullConfig = results[0];
            allUsers = results[1] || [];
            allLibraries = results[2] || [];
            allDevices = results[3] || [];
            parentalRatings = results[4] || [];
            allCultures = results[5] || [];
            allCastReceivers = results[6] || [];
            allViews = results[7] || [];
            allGroupable = results[8] || [];
            if (!fullConfig.Groups) fullConfig.Groups = [];

            renderSections();
            ensureMemberSelector();
            populateDropdown();
            if (tracker) tracker.markClean();
            renderUserCards();
        }).catch(function () {
            Shared.setStatus('groupStatus', 'Could not load the groups. Reload the page.', true);
        });
    }

    // ratingScore
    // Reads a rating's score from either API shape.
    //
    // Inputs
    // r [object]: The rating.
    //
    // Output
    // [number]: The score.
    function ratingScore(r) {
        var rs = r.RatingScore || r.ratingScore;
        if (rs) return rs.Score != null ? rs.Score : rs.score;
        return r.Value != null ? r.Value : r.value;
    }
    // ratingSubScore
    // Reads a rating's sub score, when it has one.
    //
    // Inputs
    // r [object]: The rating.
    //
    // Output
    // [number]: The sub score, or null.
    function ratingSubScore(r) {
        var rs = r.RatingScore || r.ratingScore;
        if (rs) return rs.SubScore != null ? rs.SubScore : rs.subScore;
        return null;
    }
    // ratingName
    // Reads a rating's name from either API shape.
    //
    // Inputs
    // r [object]: The rating.
    //
    // Output
    // [string]: The name.
    function ratingName(r) { return r.Name || r.name || ''; }

    // renderSectionGroup
    // Draws one collapsible section per entry, each holding its setting rows.
    //
    // Inputs
    // sections [Array]: The PERMISSIONS or CONFIG list.
    // container [HTMLElement]: Where the sections go.
    // prefix [string]: The id prefix of each section body.
    // store [string]: perm or config, the part of the group the rows save to.
    function renderSectionGroup(sections, container, prefix, store) {
        var esc = Shared.escapeHtml;
        var html = '';

        sections.forEach(function (section, si) {
            var id = prefix + si;
            html += '<div class="base-collapsible-section">'
                + '<button type="button" class="base-collapsible-header collapsed" aria-expanded="false" data-target="' + id + '">'
                + '<span class="base-collapsible-title">' + esc(section.title) + '</span>'
                + '<span class="base-collapsible-icon">&#9660;</span></button>'
                + '<div id="' + id + '" class="base-collapsible-content collapsed">';

            section.perms.forEach(function (p) {
                html += renderPermRow(p, store);
            });

            html += '</div></div>';
        });

        container.innerHTML = html;
    }

    // renderSections
    // Draws every setting row, then mounts the Base components the rows hold.
    function renderSections() {
        destroyRowHandles();
        renderSectionGroup(PERMISSIONS, view.querySelector('#permissionSections'), 'permSection', 'perm');
        renderSectionGroup(CONFIG, view.querySelector('#configSections'), 'configSection', 'config');
        Shared.initCollapsibles();
        mountRowComponents();
        mountInheritFields();
        bindPermRows();
    }

    // mountInheritFields
    // Wraps each setting's control in an Inherit Field with an Override checkbox. The control keeps its own
    // value in the page, so the field handle reads and writes nothing, and the Inherit Field only dims and
    // disables the control while the setting is not overridden.
    function mountInheritFields() {
        view.querySelectorAll('.perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var control = row.querySelector('.perm-control');
            inheritHandles[key] = createInheritField({
                field: { element: control, getValue: function () { return null; }, setValue: function () {} },
                toggle: 'checkbox',
                overrideText: 'Override',
                onChange: function (value) { if (!value.inherit) refreshManagedRow(row); }
            });
        });
    }

    // isManaged
    // Says whether a setting is overridden.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
    //
    // Output
    // [boolean]: Whether the group's value applies.
    function isManaged(row) {
        var handle = inheritHandles[row.getAttribute('data-perm')];
        return !!handle && !handle.getValue().inherit;
    }

    // setManaged
    // Sets whether a setting is overridden. It passes through Override first, so a control that a component drew
    // again while inheriting, such as an Ordered List's move buttons, is disabled with the rest.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
    // managed [boolean]: Whether the group's value applies.
    function setManaged(row, managed) {
        var handle = inheritHandles[row.getAttribute('data-perm')];
        if (!handle) return;
        handle.setValue({ inherit: false });
        if (managed) refreshManagedRow(row);
        else handle.setValue({ inherit: true });
    }

    // refreshManagedRow
    // Redraws what an overridden row hides or disables by itself.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
    function refreshManagedRow(row) {
        updateFolderVisibility(row);
        // A redraw puts back the move buttons the ends of the list keep disabled.
        var order = row.getAttribute('data-type') === 'order' ? rowHandles[row.getAttribute('data-perm')] : null;
        if (order) order.setItems(order.getItems());
    }

    // destroyRowHandles
    // Tears down the Base components of the last draw.
    function destroyRowHandles() {
        Object.keys(inheritHandles).forEach(function (key) { inheritHandles[key].destroy(); });
        inheritHandles = {};
        Object.keys(rowHandles).forEach(function (key) { rowHandles[key].destroy(); });
        rowHandles = {};
        if (sessionRules) {
            sessionRules.destroy();
            if (sessionRules.element.parentNode) sessionRules.element.parentNode.removeChild(sessionRules.element);
            sessionRules = null;
        }
    }

    // mountRowComponents
    // Mounts a Tag Input in each tags row, an Access Schedule in the schedule row, and an Ordered List in the library order row.
    function mountRowComponents() {
        view.querySelectorAll('.perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var mount = row.querySelector('.perm-mount');
            var handle = null;
            if (type === 'tags') {
                handle = createTagInput({ placeholder: 'Kids', onChange: checkDirty });
            } else if (type === 'schedule') {
                handle = createAccessSchedule({ addText: 'Add Schedule', onChange: checkDirty });
            } else if (type === 'order') {
                handle = createOrderedList({
                    render: function (v) { return Shared.escapeHtml(v.name); },
                    label: function (v) { return v.name; },
                    onMove: function (from, to, items) { mergeOrder(items); checkDirty(); }
                });
            }
            if (handle && mount) {
                mount.appendChild(handle.element);
                rowHandles[key] = handle;
            }
        });
    }

    // checkGrid
    // Builds a Check Grid of checkboxes.
    //
    // Inputs
    // gridClass [string]: The class that names the grid.
    // itemClass [string]: The class that names each checkbox.
    // items [Array]: The items to list.
    // valueOf [function]: Reads an item's value.
    // nameOf [function]: Reads an item's label.
    //
    // Output
    // [string]: The grid's HTML.
    function checkGrid(gridClass, itemClass, items, valueOf, nameOf) {
        var esc = Shared.escapeHtml;
        return '<div class="base-check-grid ' + gridClass + '">'
            + items.map(function (it) {
                return '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="' + itemClass + '" value="'
                    + esc(valueOf(it)) + '" /><span class="checkboxLabel">' + esc(nameOf(it)) + '</span></label>';
            }).join('')
            + '</div>';
    }

    // libraryGrid
    // Builds a Check Grid of every library.
    //
    // Inputs
    // itemClass [string]: The class that names each checkbox.
    //
    // Output
    // [string]: The grid's HTML.
    function libraryGrid(itemClass) {
        return checkGrid('perm-folders', itemClass, allLibraries,
            function (l) { return l.ItemId; }, function (l) { return l.Name; });
    }

    // cultureOptions
    // Builds the options of a language select.
    //
    // Output
    // [string]: The options' HTML.
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

    // orderSource
    // Lists the home screen views in their default order, falling back to the libraries when there are no views.
    //
    // Output
    // [Array]: One { id, name } per view.
    function orderSource() {
        if (allViews && allViews.length) {
            return allViews.map(function (v) { return { id: v.Id || v.id, name: v.Name || v.name || '' }; });
        }
        return allLibraries.map(function (l) { return { id: l.ItemId, name: l.Name }; });
    }

    // mergeOrder
    // Writes a new order of the shown views back into the full order, keeping hidden views in their places.
    //
    // Inputs
    // shown [Array]: The shown views in their new order.
    function mergeOrder(shown) {
        var shownIds = {};
        shown.forEach(function (v) { shownIds[v.id] = true; });
        var next = shown.slice();
        orderState = orderState.map(function (v) { return shownIds[v.id] ? next.shift() : v; });
    }

    // renderPermRow
    // Builds one setting row, with its Override checkbox, label, control, and description.
    //
    // Inputs
    // p [object]: The setting from PERMISSIONS or CONFIG.
    // store [string]: perm or config.
    //
    // Output
    // [string]: The row's HTML.
    function renderPermRow(p, store) {
        var esc = Shared.escapeHtml;
        var control = '';

        if (p.type === 'bool') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-value" />'
                + '<span class="checkboxLabel">Enabled</span></label>';
        } else if (p.type === 'int') {
            control = '<input is="emby-input" type="number" min="' + (p.min != null ? p.min : 0) + '" class="perm-value" aria-label="' + esc(p.label) + '" />';
        } else if (p.type === 'bitrate') {
            control = '<input is="emby-input" type="number" min="0" step="0.1" class="perm-value" aria-label="' + esc(p.label) + '" />';
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
            control = '<div class="base-check-grid perm-unrated-grid">' + UNRATED_ITEMS.map(function (u) {
                return '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-unrated" value="'
                    + esc(u[0]) + '" /><span class="checkboxLabel">' + esc(u[1]) + '</span></label>';
            }).join('') + '</div>';
        } else if (p.type === 'tags' || p.type === 'schedule' || p.type === 'order') {
            control = '<div class="perm-mount"></div>';
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
        } else if (p.type === 'homesections') {
            var secOpts = HOME_SECTIONS.map(function (o) {
                return '<option value="' + esc(o[0]) + '">' + esc(o[1]) + '</option>';
            }).join('');
            var secRows = '';
            for (var hi = 0; hi < HOME_SECTION_COUNT; hi++) {
                secRows += '<div class="selectContainer">'
                    + '<label class="selectLabel" for="permHomeSection' + hi + '">Home screen section ' + (hi + 1) + '</label>'
                    + '<select is="emby-select" id="permHomeSection' + hi + '" class="perm-homesection emby-select-withcolor" data-index="' + hi + '">' + secOpts + '</select>'
                    + '</div>';
            }
            control = secRows;
        } else if (p.type === 'libgrid') {
            // GroupedFolders lists only the folders Jellyfin can group. The exclude grids list every library.
            var gridItems = (p.source === 'groupable') ? allGroupable : allLibraries;
            var idOf = (p.source === 'groupable')
                ? function (v) { return v.Id || v.id; }
                : function (l) { return l.ItemId; };
            control = checkGrid('perm-lib-grid', 'perm-lib', gridItems,
                idOf, function (v) { return v.Name || v.name || ''; });
        }

        var invertAttr = (p.type === 'libgrid') ? ' data-invert="' + (p.invert ? '1' : '0') + '"' : '';
        var defAttr = p.def ? ' data-default="1"' : '';
        var attrs = ' data-store="' + esc(store) + '" data-perm="' + esc(p.key) + '" data-type="' + esc(p.type) + '"' + invertAttr + defAttr;

        // A checkbox setting reads as Jellyfin's own checkbox, with the setting as its label.
        if (p.type === 'bool') {
            return '<div class="checkboxContainer checkboxContainer-withDescription perm-row"' + attrs + '>'
                + '<div class="perm-control"><label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-value" />'
                + '<span class="checkboxLabel">' + esc(p.label) + '</span></label></div>'
                + (p.desc ? '<div class="fieldDescription checkboxFieldDescription">' + esc(p.desc) + '</div>' : '')
                + '</div>';
        }

        // A single input or select takes the label's for, so the label names it.
        var single = /^<(input|select)\b/.test(control) && control.indexOf('<select', 1) === -1;
        var controlId = 'perm' + store + p.key;
        if (single) control = control.replace(/^<(input|select)\b/, '<$1 id="' + controlId + '"');
        var labelHtml = p.label
            ? '<label class="inputLabel"' + (single ? ' for="' + controlId + '"' : '') + '>' + esc(p.label) + '</label>'
            : '';
        // Not an inputContainer, whose rule sizes every input in it full width, the Override checkbox included.
        return '<div class="um-setting perm-row"' + attrs + '>'
            + labelHtml
            + '<div class="perm-control">' + control + '</div>'
            + (p.desc ? '<div class="fieldDescription">' + esc(p.desc) + '</div>' : '')
            + '</div>';
    }

    // bindPermRows
    // Wires each row's Override checkbox and its own controls.
    function bindPermRows() {
        view.querySelectorAll('.perm-row').forEach(function (row) {
            var type = row.getAttribute('data-type');

            row.querySelectorAll('.perm-allfolders, .perm-alldelete, .perm-alldevices').forEach(function (toggle) {
                toggle.addEventListener('change', function () { updateFolderVisibility(row); });
            });

            // The home screen library lists follow Library Access, so they are filtered again on each change.
            if (row.getAttribute('data-perm') === 'LibraryAccess') {
                row.addEventListener('change', applyAccessFilter);
            }

            if (type === 'submode') {
                var sel = row.querySelector('.perm-value');
                sel.addEventListener('change', function () { updateSubmodeHelp(row); });
            }
        });
    }

    // updateSubmodeHelp
    // Shows the sentence for the picked subtitle mode.
    //
    // Inputs
    // row [HTMLElement]: The subtitle mode row.
    function updateSubmodeHelp(row) {
        var help = row.querySelector('.perm-submode-help');
        if (!help) return;
        var val = row.querySelector('.perm-value').value;
        var mode = SUBTITLE_MODES.find(function (m) { return m[0] === val; });
        help.textContent = mode ? mode[2] : '';
    }

    // updateFolderVisibility
    // Hides a check grid while its All checkbox is on.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
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
                grid.classList.toggle('hidden', all.checked);
            }
        });
    }

    // groupLabel
    // Names a group in the select, marking the default.
    //
    // Inputs
    // g [object]: The group.
    //
    // Output
    // [string]: The label.
    function groupLabel(g) {
        return (g.Name || 'Unnamed group') + (g.Id === fullConfig.DefaultGroupId ? ' (Default)' : '');
    }

    // refreshGroupLabels
    // Writes every group's label in the select again.
    function refreshGroupLabels() {
        var select = view.querySelector('#selectGroup');
        if (!select) return;
        Array.prototype.forEach.call(select.options, function (opt) {
            var g = fullConfig.Groups.find(function (x) { return x.Id === opt.value; });
            if (g) opt.textContent = groupLabel(g);
        });
    }

    // updateDefaultButton
    // Shows whether the picked group is the default on the Default button.
    function updateDefaultButton() {
        var btn = view.querySelector('#btnDefaultGroup');
        if (!btn) return;
        var isDefault = !!currentGroupId && currentGroupId === fullConfig.DefaultGroupId;
        btn.classList.toggle('primary', isDefault);
        btn.classList.toggle('secondary', !isDefault);
        var icon = btn.querySelector('.material-icons');
        if (icon) icon.textContent = isDefault ? 'star' : 'star_border';
        btn.setAttribute('title', isDefault ? 'Unset Default' : 'Set as Default');
    }

    // toggleDefaultGroup
    // Makes the picked group the default, or unsets it when it already is.
    function toggleDefaultGroup() {
        if (!currentGroupId) return;
        fullConfig.DefaultGroupId = (fullConfig.DefaultGroupId === currentGroupId) ? null : currentGroupId;
        refreshGroupLabels();
        updateDefaultButton();
        checkDirty();
    }

    // populateDropdown
    // Fills the group select, keeping the picked group when it still exists.
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

    // getCurrentGroup
    // Finds the picked group.
    //
    // Output
    // [object]: The group, or undefined.
    function getCurrentGroup() {
        return fullConfig.Groups.find(function (g) { return g.Id === currentGroupId; });
    }

    // serializeGroups
    // Reads the form into the config and writes it as text, for the Change Tracker.
    //
    // Output
    // [string]: The config as JSON, or empty before it loads.
    function serializeGroups() {
        if (!fullConfig) return '';
        if (currentGroupId) collectCurrentGroup();
        return JSON.stringify(fullConfig);
    }

    // checkDirty
    // Shows or hides the unsaved indicator.
    function checkDirty() {
        // A component that reports its own setValue must not read a half loaded group back.
        if (tracker && !loading) tracker.check();
    }

    // persistGroups
    // Saves the groups and the default group onto the latest config.
    //
    // Output
    // [Promise]: The save result.
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

    // loadConfigRow
    // Fills one preference row from the group's configuration.
    //
    // Inputs
    // row [HTMLElement]: The row.
    // cfg [object]: The group's configuration.
    // key [string]: The setting.
    // type [string]: The row's control type.
    // manage [object]: Takes the Override state in checked.
    function loadConfigRow(row, cfg, key, type, manage) {
        manage.checked = cfg['Manage' + key] === true;

        if (type === 'order') {
            // Saved views go first in their saved order, and the rest follow in the default order.
            var views = orderSource();
            var byId = {};
            views.forEach(function (v) { byId[v.id] = v; });
            var front = (cfg.OrderedViews || []).filter(function (id) { return byId[id]; }).map(function (id) { return byId[id]; });
            orderState = front.concat(views.filter(function (v) { return front.indexOf(v) === -1; }));
        } else if (type === 'libgrid') {
            var invert = row.getAttribute('data-invert') === '1';
            var stored = cfg[key] || [];
            row.querySelectorAll('.perm-lib').forEach(function (cb) {
                var listed = stored.indexOf(cb.value) !== -1;
                cb.checked = invert ? !listed : listed;
            });
        } else if (type === 'homesections') {
            var secs = (cfg.HomeSections && cfg.HomeSections.length) ? cfg.HomeSections : DEFAULT_HOME_SECTIONS;
            row.querySelectorAll('.perm-homesection').forEach(function (sel, idx) {
                sel.value = secs[idx] || 'none';
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

    // collectConfigRow
    // Reads one preference row into the group's configuration.
    //
    // Inputs
    // row [HTMLElement]: The row.
    // cfg [object]: The group's configuration.
    // key [string]: The setting.
    // type [string]: The row's control type.
    // managed [boolean]: Whether the row is overridden.
    function collectConfigRow(row, cfg, key, type, managed) {
        cfg['Manage' + key] = managed;

        if (type === 'order') {
            // Only the libraries members can see are shown, so only those are saved in the order.
            var order = rowHandles[key];
            cfg.OrderedViews = order ? order.getItems().map(function (v) { return v.id; }) : [];
        } else if (type === 'libgrid') {
            var invert = row.getAttribute('data-invert') === '1';
            var out = [];
            row.querySelectorAll('.perm-lib').forEach(function (cb) {
                var label = cb.closest('.emby-checkbox-label');
                if (label && label.classList.contains('hidden')) return;
                if (invert ? !cb.checked : cb.checked) out.push(cb.value);
            });
            cfg[key] = out;
        } else if (type === 'homesections') {
            var secs = [];
            row.querySelectorAll('.perm-homesection').forEach(function (sel) { secs.push(sel.value); });
            cfg.HomeSections = secs;
        } else if (type === 'submode' || type === 'culture' || type === 'castreceiver') {
            cfg[key] = row.querySelector('.perm-value').value;
        } else {
            cfg[key] = row.querySelector('.perm-value').checked;
        }
    }

    // accessibleLibraryIds
    // Reads the libraries members can see from the Library Access row.
    //
    // Output
    // [object]: A set of library ids, or null when every library is visible.
    function accessibleLibraryIds() {
        var row = view.querySelector('.perm-row[data-perm="LibraryAccess"]');
        if (!row) return null;
        var managed = isManaged(row);
        var allFolders = row.querySelector('.perm-allfolders').checked;
        if (!managed || allFolders) return null;
        var set = {};
        row.querySelectorAll('.perm-folder:checked').forEach(function (cb) { set[cb.value] = true; });
        return set;
    }

    // applyAccessFilter
    // Hides the home screen libraries members cannot see, so an admin cannot order or toggle a hidden library.
    function applyAccessFilter() {
        var access = accessibleLibraryIds();
        var accessible = function (id) { return access === null || access[id] === true; };
        // Derived views such as Live TV and Collections are not libraries, so they always stay.
        var libIds = {};
        allLibraries.forEach(function (l) { libIds[l.ItemId] = true; });

        view.querySelectorAll('#configSections .perm-row').forEach(function (row) {
            var type = row.getAttribute('data-type');
            if (type === 'order') {
                var order = rowHandles[row.getAttribute('data-perm')];
                if (order) {
                    order.setItems(orderState.filter(function (v) { return libIds[v.id] !== true || accessible(v.id); }));
                    setManaged(row, isManaged(row));
                }
            } else if (type === 'libgrid') {
                row.querySelectorAll('.perm-lib').forEach(function (cb) {
                    var label = cb.closest('.emby-checkbox-label');
                    if (label) label.classList.toggle('hidden', !accessible(cb.value));
                });
            }
        });
    }

    // loadCurrentGroup
    // Fills every row from the picked group.
    function loadCurrentGroup() {
        updateDefaultButton();
        var group = getCurrentGroup();
        if (!group) return;
        loading = true;
        var perms = group.Permissions || {};
        var cfg = group.Configuration || {};

        view.querySelectorAll('.perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var manage = { checked: false };

            if (row.getAttribute('data-store') === 'config') {
                loadConfigRow(row, cfg, key, type, manage);
                setManaged(row, manage.checked);
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
                if (rowHandles[key]) rowHandles[key].setValue(perms[key] || []);
            } else if (type === 'schedule') {
                manage.checked = perms.ManageAccessSchedules === true;
                if (rowHandles[key]) rowHandles[key].setValue(perms.AccessSchedules || []);
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

            setManaged(row, manage.checked);
        });

        applyAccessFilter();
        loadGroupExtras(group);
        if (memberSel) {
            refreshMemberAvailability(group);
        }
        updateMemberSummary();
        loading = false;
    }

    // refreshMemberAvailability
    // Marks users who belong to another group as disabled, with that group's name.
    // A user belongs to one group at a time, so they move by leaving the other group first.
    //
    // Inputs
    // group [object]: The picked group.
    function refreshMemberAvailability(group) {
        var notes = {};
        var others = [];
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

    // loadGroupExtras
    // Fills the Other sections from the group.
    //
    // Inputs
    // group [object]: The picked group.
    function loadGroupExtras(group) {
        var pw = group.Password || {};
        var el = view.querySelector.bind(view);
        if (el('#grpPwEnabled')) el('#grpPwEnabled').checked = pw.Enabled === true;
        if (el('#grpPwLength')) el('#grpPwLength').value = pw.MinLength || 8;
        // A group with no saved policy starts checked, as the server does.
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

        if (el('#grpSessionEnabled')) el('#grpSessionEnabled').checked = group.CleanupSessions === true;
        renderSessionRules(group.SessionCleanupRules || []);
        updateSessionEnabledState();
    }

    // collectGroupExtras
    // Reads the Other sections into the group.
    //
    // Inputs
    // group [object]: The picked group.
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

        group.CleanupSessions = !!(el('#grpSessionEnabled') || {}).checked;
        if (sessionRules) group.SessionCleanupRules = domSessionRules();
    }

    var CHANGE_MODES = ['Allowed', 'InitialOnly', 'Disallowed'];

    // normalizeChangeMode
    // Reads a password change mode, which the config endpoint may send as a number or a name.
    //
    // Inputs
    // value [number|string]: The stored mode.
    //
    // Output
    // [string]: Allowed, InitialOnly, or Disallowed.
    function normalizeChangeMode(value) {
        if (typeof value === 'number') return CHANGE_MODES[value] || 'Allowed';
        return CHANGE_MODES.indexOf(value) >= 0 ? value : 'Allowed';
    }

    // updatePwEnabledState
    // Shows the password fields while password requirements are on.
    function updatePwEnabledState() {
        Shared.setVisible('pwFields', !!(view.querySelector('#grpPwEnabled') || {}).checked);
    }

    // updateExpiryEnabledState
    // Shows the expiry fields while expiry is on.
    function updateExpiryEnabledState() {
        Shared.setVisible('grpExpiryFields', !!(view.querySelector('#grpExpiryEnabled') || {}).checked);
    }

    // updateInactiveEnabledState
    // Shows the inactivity fields while inactivity is on.
    function updateInactiveEnabledState() {
        Shared.setVisible('inactiveFields', !!(view.querySelector('#grpInactiveEnabled') || {}).checked);
    }

    var SESSION_MODES = ['All', 'Only', 'AllExcept'];

    // normalizeSessionMode
    // Reads a session client mode, which the config endpoint may send as a number or a name.
    //
    // Inputs
    // value [number|string]: The stored mode.
    //
    // Output
    // [string]: All, Only, or AllExcept.
    function normalizeSessionMode(value) {
        if (typeof value === 'number') return SESSION_MODES[value] || 'All';
        return SESSION_MODES.indexOf(value) >= 0 ? value : 'All';
    }

    // knownClients
    // Lists the clients with a device on the server, plus the clients the rules name, so a client whose devices are gone stays listed.
    //
    // Inputs
    // rules [Array]: The session rules.
    //
    // Output
    // [Array]: The client names, sorted.
    function knownClients(rules) {
        var seen = {};
        var out = [];
        var add = function (name) {
            if (name && !seen[name.toLowerCase()]) { seen[name.toLowerCase()] = true; out.push(name); }
        };
        allDevices.forEach(function (d) { add(d.AppName || d.appName); });
        (rules || []).forEach(function (r) { (r.Clients || []).forEach(add); });
        return out.sort(function (a, b) { return a.localeCompare(b); });
    }

    var SESSION_MODE_OPTIONS = [
        { value: 'All', label: 'All Clients' },
        { value: 'Only', label: 'Only These Clients' },
        { value: 'AllExcept', label: 'All Except These Clients' }
    ];

    // clientField
    // Draws a rule's clients as a Chip Select of the known clients, for the Repeater's custom field.
    //
    // Inputs
    // value [Array]: The rule's client names.
    // change [function]: Tells the Repeater the value changed.
    //
    // Output
    // [object]: The Repeater control, { element, input, getValue, setValue, destroy }.
    function clientField(value, change) {
        var chips = createChipSelect({
            options: sessionClients,
            value: value || [],
            placeholder: 'Search clients',
            noResultsText: 'No clients match.',
            onChange: change
        });
        return {
            element: chips.element,
            input: chips.element.querySelector('input'),
            getValue: function () { return chips.getValue().map(String); },
            setValue: function (next) { chips.setValue(next || []); },
            destroy: chips.destroy
        };
    }

    // ensureSessionRules
    // Draws the session cleanup rules once, as a Repeater of cards.
    function ensureSessionRules() {
        if (sessionRules) return;
        sessionRules = createRepeater({
            layout: 'card',
            titleText: 'Rule {index}',
            addText: 'Add Rule',
            removeText: 'Remove {value}',
            emptyText: 'No rules yet. Select Add Rule to add one.',
            fields: [
                { key: 'mode', label: 'Applies to', type: 'select', options: SESSION_MODE_OPTIONS },
                { key: 'clients', label: 'Clients', type: 'custom', render: clientField },
                { key: 'days', label: 'Days before logout', type: 'number', min: 1, placeholder: '30' }
            ],
            onChange: function () {
                updateSessionOverlaps();
                checkDirty();
            }
        });
        view.querySelector('#sessionRuleList').appendChild(sessionRules.element);
    }

    // renderSessionRules
    // Fills the session cleanup rules from the group.
    //
    // Inputs
    // rules [Array]: The rules.
    function renderSessionRules(rules) {
        sessionClients = knownClients(rules);
        ensureSessionRules();
        sessionRules.setValue((rules || []).map(function (r) {
            return { mode: normalizeSessionMode(r.Mode), clients: (r.Clients || []).slice(), days: r.Days > 0 ? r.Days : 30 };
        }));
        updateSessionOverlaps();
    }

    // domSessionRules
    // Reads the Repeater back into rules. A rule for all clients keeps no client list.
    //
    // Output
    // [Array]: The rules.
    function domSessionRules() {
        return (sessionRules ? sessionRules.getValue() : []).map(function (item) {
            var mode = normalizeSessionMode(item.mode);
            var days = parseInt(item.days, 10);
            return {
                Mode: mode,
                Clients: mode === 'All' ? [] : (item.clients || []).map(String),
                Days: isNaN(days) || days < 1 ? 30 : days
            };
        });
    }

    // updateSessionOverlaps
    // Names the clients more than one rule covers, since the shortest window wins there.
    function updateSessionOverlaps() {
        var warn = view.querySelector('#sessionOverlapWarning');
        if (!warn) return;
        var rules = domSessionRules();
        var overlapped = [];
        knownClients(rules).forEach(function (client) {
            var matches = rules.filter(function (r) {
                if (r.Mode === 'All') return true;
                var listed = r.Clients.some(function (c) { return c.toLowerCase() === client.toLowerCase(); });
                return r.Mode === 'Only' ? listed : !listed;
            });
            if (matches.length > 1) overlapped.push(client);
        });
        warn.textContent = overlapped.length
            ? 'More than one rule covers ' + overlapped.join(', ') + '. The shortest window applies where rules overlap.'
            : '';
        warn.classList.toggle('hidden', overlapped.length === 0);
    }

    // updateSessionEnabledState
    // Shows the rules while session cleanup is on.
    function updateSessionEnabledState() {
        Shared.setVisible('sessionFields', !!(view.querySelector('#grpSessionEnabled') || {}).checked);
    }

    // renderUserCards
    // Draws the count cards. Inactive counts the members the expiry task would disable, by each group's own window.
    // Members who have never signed in are left alone, as the task does.
    function renderUserCards() {
        if (!view.querySelector('#userCards')) return;
        var cutoffFor = {};
        (fullConfig.Groups || []).forEach(function (g) {
            if (g.DisableInactiveUsers !== true || !(g.InactiveDays > 0)) return;
            var cutoff = Date.now() - g.InactiveDays * 86400000;
            (g.MemberIds || []).forEach(function (id) {
                if (cutoffFor[id] === undefined || cutoff > cutoffFor[id]) cutoffFor[id] = cutoff;
            });
        });

        var admins = 0, active = 0, inactive = 0, disabled = 0;
        var adminIds = {};
        allUsers.forEach(function (u) {
            var p = u.Policy || {};
            if (p.IsAdministrator) { admins++; adminIds[u.Id] = true; return; }
            if (p.IsDisabled) { disabled++; return; }
            var cutoff = cutoffFor[u.Id];
            var last = u.LastActivityDate || u.LastLoginDate;
            if (cutoff !== undefined && last && new Date(last).getTime() < cutoff) { inactive++; }
            else { active++; }
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

        Shared.renderCards('userCards', [
            { count: admins, label: 'Admins', color: 'blue' },
            { count: active, label: 'Active', color: 'green' },
            { count: inactive, label: 'Inactive', color: 'yellow' },
            { count: disabled, label: 'Disabled', color: 'red' },
            { count: Object.keys(expiringIds).length, label: 'Expiring', color: 'purple' }
        ]);
    }

    // collectCurrentGroup
    // Reads every row into the picked group.
    function collectCurrentGroup() {
        var group = getCurrentGroup();
        // Once the page hides, its rows are gone, so there is nothing to read back.
        if (!group || !Object.keys(inheritHandles).length) return;
        if (!group.Permissions) group.Permissions = {};
        if (!group.Configuration) group.Configuration = {};
        var perms = group.Permissions;
        var cfg = group.Configuration;

        view.querySelectorAll('.perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var managed = isManaged(row);

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
                perms[key] = rowHandles[key] ? rowHandles[key].getValue() : [];
            } else if (type === 'schedule') {
                perms.ManageAccessSchedules = managed;
                perms.AccessSchedules = rowHandles[key] ? rowHandles[key].getValue() : [];
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

    // ensureMemberSelector
    // Draws the member picker once, and refreshes it on later loads.
    function ensureMemberSelector() {
        var mount = view.querySelector('#memberMount');
        if (!mount) return;
        if (memberSel) { memberSel.refresh(); return; }
        memberSel = createUserMultiSelector({
            adminFilter: 'exclude',
            showAvatars: true,
            maxHeight: 280,
            fetchUsers: function () { return allUsers; },
            emptyMessage: 'No users available.',
            disabledIds: [],
            noteFor: function (u) { return memberNotes[u.id] || ''; },
            onChange: onMembersChanged
        });
        mount.appendChild(memberSel.element);
    }

    // onMembersChanged
    // Saves the picked members on the group and takes them out of every other group.
    //
    // Inputs
    // ids [Array]: The picked user ids.
    function onMembersChanged(ids) {
        var group = getCurrentGroup();
        if (!group) return;
        group.MemberIds = (ids || []).slice();
        fullConfig.Groups.forEach(function (g) {
            if (g.Id === currentGroupId) return;
            g.MemberIds = (g.MemberIds || []).filter(function (x) { return group.MemberIds.indexOf(x) === -1; });
        });
        updateMemberSummary();
        checkDirty();
    }

    // updateMemberSummary
    // Writes the member count.
    function updateMemberSummary() {
        var group = getCurrentGroup();
        var n = group ? (group.MemberIds || []).length : 0;
        var el = view.querySelector('#memberSummary');
        if (el) el.textContent = Shared.plural(n, 'member') + ' in this group';
    }

    // askName
    // Asks for a group name in a Prompt Dialog, refusing an empty or taken name.
    //
    // Inputs
    // title [string]: The dialog title.
    // value [string]: The name to start with.
    // exceptId [string]: The group being renamed, or null.
    //
    // Output
    // [Promise]: The trimmed name, or null when cancelled.
    function askName(title, value, exceptId) {
        return promptDialog([{
            name: 'name',
            label: 'Name',
            value: value || '',
            required: true,
            validate: function (v) {
                return nameExists((v || '').trim(), exceptId) ? 'A group with this name already exists.' : '';
            }
        }], { title: title }).then(function (result) {
            var name = result ? (result.name || '').trim() : '';
            return name || null;
        });
    }

    // nameExists
    // Says whether another group already has a name, ignoring case.
    //
    // Inputs
    // name [string]: The name.
    // exceptId [string]: The group to leave out, or null.
    //
    // Output
    // [boolean]: Whether the name is taken.
    function nameExists(name, exceptId) {
        return fullConfig.Groups.some(function (g) {
            return g.Id !== exceptId && (g.Name || '').toLowerCase() === name.toLowerCase();
        });
    }

    // newGroup
    // Asks for a name and adds an empty group.
    function newGroup() {
        askName('New Group', '', null).then(function (name) {
            if (!name) return;
            if (currentGroupId) collectCurrentGroup();
            var group = { Id: generateGuid(), Name: name, MemberIds: [], Permissions: {}, Configuration: {} };
            fullConfig.Groups.push(group);
            currentGroupId = group.Id;
            populateDropdown();
            checkDirty();
        });
    }

    // renameGroup
    // Asks for a new name for the picked group.
    function renameGroup() {
        var group = getCurrentGroup();
        if (!group) return;
        askName('Rename Group', group.Name, group.Id).then(function (name) {
            if (!name) return;
            group.Name = name;
            populateDropdown();
            checkDirty();
        });
    }

    // deleteGroup
    // Asks first, then deletes the picked group. Nothing is saved until Save.
    function deleteGroup() {
        var group = getCurrentGroup();
        if (!group) return;
        confirmDialog('Delete the group ' + group.Name + '? Its members are unassigned.', {
            title: 'Delete Group',
            confirmText: 'Delete',
            destructive: true
        }).then(function (ok) {
            if (!ok) return;
            if (fullConfig.DefaultGroupId === group.Id) fullConfig.DefaultGroupId = null;
            fullConfig.Groups = fullConfig.Groups.filter(function (g) { return g.Id !== group.Id; });
            currentGroupId = null;
            populateDropdown();
            checkDirty();
        });
    }

    // confirmDeleteExpiry
    // Asks first when the picked group deletes its members on expiry, since that cannot be undone.
    //
    // Output
    // [Promise]: Whether to go on with the save.
    function confirmDeleteExpiry() {
        var cur = getCurrentGroup();
        if (!(cur && cur.ExpiresOn && cur.ExpiryAction === 'Delete')) return Promise.resolve(true);
        return confirmDialog('Save ' + (cur.Name || 'this group') + '? It deletes its members on '
            + String(cur.ExpiresOn).slice(0, 10) + ', and this cannot be undone.', {
            title: 'Delete Members on Expiry',
            confirmText: 'Save',
            destructive: true
        });
    }

    // save
    // Saves the groups and applies them to their members.
    function save() {
        if (currentGroupId) collectCurrentGroup();

        confirmDeleteExpiry().then(function (ok) {
            if (!ok) return;
            Shared.setStatus('groupStatus', 'Saving…', false);
            persistGroups().then(function () {
                return Shared.apiRequest('Apply', 'POST');
            }).then(function () {
                if (tracker) tracker.markClean();
                renderUserCards();
                Shared.setStatus('groupStatus', 'Saved and applied to members.', false);
            }).catch(function () {
                Shared.setStatus('groupStatus', 'Could not save the groups.', true);
            });
        });
    }

    // bind
    // Wires the page once and starts the Change Tracker.
    //
    // Inputs
    // shared [object]: The helper bag from createPage.
    function bind(shared) {
        tracker = shared.createChangeTracker({ serialize: serializeGroups, indicatorId: 'unsavedIndicator' });
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

        var sessionEnabled = view.querySelector('#grpSessionEnabled');
        if (sessionEnabled) sessionEnabled.addEventListener('change', updateSessionEnabledState);

        var form = view.querySelector('#UserManagementGroupsForm');
        if (form) {
            form.addEventListener('change', checkDirty);
            form.addEventListener('input', checkDirty);
        }
    }

    Shared = createPage(view, 0, {
        bind: bind,
        load: loadAll,
        onHide: destroyRowHandles,
        isDirty: function () { return !!tracker && tracker.isDirty(); }
    }).shared;
}
