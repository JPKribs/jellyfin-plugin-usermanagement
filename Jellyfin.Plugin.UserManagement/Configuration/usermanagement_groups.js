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
    createCheckboxList,
    createList,
    setCollapsibleBadge,
    confirmDialog,
    promptDialog,
    createSaveBar
} from './configurationpage?name=usermanagement_shared.js';

// Groups page
// Builds the Groups tab. Each setting row is an Inherit Field whose pills read Member and Group, and only
// settings on Group reach members.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
export default function (view) {
    'use strict';

    var Shared = null;
    var memberSel = null;
    var memberNotes = {};
    var tracker = null;
    var saveBar = null;
    var rowHandles = {};
    var inheritHandles = {};
    var rowLists = [];
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
    var allChannels = [];
    var parentalRatings = [];
    var allCultures = [];
    var allCastReceivers = [];
    var allViews = [];
    var allGroupable = [];

    var UNRATED_ITEMS = [
        ['Book', 'Books'], ['ChannelContent', 'Channels'], ['LiveTvChannel', 'Live TV'],
        ['Movie', 'Movies'], ['Music', 'Music'], ['Trailer', 'Trailers'], ['Series', 'Shows']
    ];

    // The two choices of every setting row. Only a setting on Group reaches members.
    var MEMBER_OWN = 'Member';
    var GROUP_SETS = 'Group';

    // Rows led by an "all" checkbox, whose list shows under it only while it is off, as Jellyfin's access page does.
    var ALL_TYPES = { library: true, channels: true, devices: true, deletion: true };

    var PERMISSIONS = [
        { title: 'Library Access', perms: [
            { key: 'LibraryAccess', label: 'Enable access to all libraries', type: 'library',
                desc: 'Turn off to pick the libraries members can open.' },
            { key: 'ChannelAccess', label: 'Enable access to all channels', type: 'channels',
                desc: 'Turn off to pick the channels members can open.' }
        ] },
        { title: 'Device Access', perms: [
            { key: 'DeviceAccess', label: 'Enable access from all devices', type: 'devices',
                desc: 'Turn off to pick the devices members can sign in from.' }
        ] },
        { title: 'Media Deletion', perms: [
            { key: 'EnableContentDeletion', label: 'Allow media deletion from all libraries', type: 'deletion',
                desc: 'Turn off to pick the libraries members can delete from.' }
        ] },
        { title: 'Media Playback', perms: [
            { key: 'EnableMediaPlayback', label: 'Allow media playback', type: 'bool' },
            { key: 'EnableAudioPlaybackTranscoding', label: 'Allow audio playback that requires transcoding', type: 'bool' },
            { key: 'EnableVideoPlaybackTranscoding', label: 'Allow video playback that requires transcoding', type: 'bool' },
            { key: 'EnablePlaybackRemuxing', label: 'Allow video playback that requires conversion without re-encoding', type: 'bool' },
            { key: 'ForceRemoteSourceTranscoding', label: 'Force transcoding of remote media sources such as Live TV', type: 'bool',
                desc: 'Limiting transcoding can make playback fail on clients that cannot play the source format.' },
            { key: 'RemoteClientBitrateLimit', label: 'Internet streaming bitrate limit (Mbps)', type: 'bitrate',
                desc: 'Limits each stream to devices outside the local network. Leave blank for no limit.' },
            { key: 'SyncPlayAccess', label: 'SyncPlay access', type: 'enum',
                desc: 'Sets whether members can watch in sync with other people.',
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
                desc: 'DLNA devices count as shared until a user starts controlling them.' }
        ] },
        { title: 'Parental Control', perms: [
            { key: 'MaxParentalRating', label: 'Maximum allowed parental rating', type: 'rating',
                desc: 'Hides content rated higher than this. Leave blank for no limit.' },
            { key: 'BlockUnratedItems', label: 'Block items with no or unrecognized rating information', type: 'unrated' },
            { key: 'AllowedTags', label: 'Allow items with tags', type: 'tags',
                desc: 'Shows only media with at least one of these tags.' },
            { key: 'BlockedTags', label: 'Block items with tags', type: 'tags',
                desc: 'Hides media with at least one of these tags.' },
            { key: 'AccessSchedules', label: 'Access schedule', type: 'schedule',
                desc: 'Limits access to these days and hours.' }
        ] },
        { title: 'Profile', perms: [
            { key: 'EnableRemoteAccess', label: 'Allow remote connections to this server', type: 'bool',
                desc: 'Blocks every remote connection while off.' },
            { key: 'EnableCollectionManagement', label: 'Allow this user to manage collections', type: 'bool' },
            { key: 'EnableSubtitleManagement', label: 'Allow this user to edit subtitles', type: 'bool' },
            { key: 'EnableLyricManagement', label: 'Allow this user to edit lyrics', type: 'bool' },
            { key: 'EnableUserPreferenceAccess', label: 'Allow this user to change their own preferences', type: 'bool',
                desc: 'While off, only an administrator or this group changes display and playback preferences.' },
            { key: 'EnableContentDownloading', label: 'Allow media downloads', type: 'bool',
                desc: 'Lets members save media to their devices. Book libraries need this to work.' },
            { key: 'EnableMediaConversion', label: 'Allow media conversion', type: 'bool' },
            { key: 'EnableSyncTranscoding', label: 'Allow sync transcoding', type: 'bool',
                desc: 'Lets the server transcode media that a client downloads for offline use.' },
            { key: 'EnablePublicSharing', label: 'Allow public sharing', type: 'bool',
                desc: 'Lets members create public share links for media.' },
            { key: 'IsDisabled', label: 'Disable this user', type: 'bool',
                desc: 'Refuses every connection from members.' },
            { key: 'IsHidden', label: 'Hide this user from login screens', type: 'bool' },
            { key: 'LoginAttemptsBeforeLockout', label: 'Failed login tries before user is locked out', type: 'int', min: -1,
                desc: 'Set 0 for the default of three tries. Set -1 to never lock out.' },
            { key: 'MaxActiveSessions', label: 'Maximum number of simultaneous user sessions', type: 'int',
                desc: 'Set 0 for no limit.' }
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

    // Display and playback preferences applied to the Jellyfin user configuration, with the labels of a user's
    // Subtitles, Playback, Display, and Home preference pages.
    var CONFIG = [
        { title: 'Subtitles', perms: [
            { key: 'SubtitleMode', label: 'Subtitle mode', type: 'submode' },
            { key: 'SubtitleLanguagePreference', label: 'Preferred subtitle language', type: 'culture' },
            { key: 'RememberSubtitleSelections', label: 'Set subtitle track based on previous item', type: 'bool',
                desc: 'Picks the subtitle track closest to the one used in the last video.' }
        ] },
        { title: 'Playback', perms: [
            { key: 'AudioLanguagePreference', label: 'Preferred audio language', type: 'culture' },
            { key: 'PlayDefaultAudioTrack', label: 'Play default audio track regardless of language', type: 'bool', def: true },
            { key: 'RememberAudioSelections', label: 'Set audio track based on previous item', type: 'bool',
                desc: 'Picks the audio track closest to the one used in the last video.' },
            { key: 'EnableNextEpisodeAutoPlay', label: 'Play next episode automatically', type: 'bool' },
            { key: 'CastReceiverId', label: 'Google Cast version', type: 'castreceiver' }
        ] },
        { title: 'Display', perms: [
            { key: 'DisplayMissingEpisodes', label: 'Display missing episodes within seasons', type: 'bool',
                desc: 'TV libraries must also have this turned on in the server settings.' },
            { key: 'DisplayCollectionsView', label: 'Display a Collections view', type: 'bool',
                desc: 'Shows a Collections entry beside the libraries on the home screen.' }
        ] },
        { title: 'Home', perms: [
            { key: 'HomeSections', label: 'Home screen sections', type: 'homesections',
                desc: 'Sets the order of the home screen sections in the web client.' },
            { key: 'HidePlayedInLatest', label: 'Hide watched content from Recently Added Media', type: 'bool', def: true },
            { key: 'OrderedViews', label: 'Media library order', type: 'order',
                desc: 'Sets the order libraries appear on the home screen.' },
            { key: 'MyMediaExcludes', label: 'Libraries on the home screen', type: 'libgrid', invert: true,
                desc: 'Picks the libraries in the My Media row.' },
            { key: 'LatestItemsExcludes', label: 'Libraries in home screen sections', type: 'libgrid', invert: true,
                desc: 'Picks the libraries in sections such as Recently Added Media and Continue Watching.' },
            { key: 'GroupedFolders', label: 'Group folders into views', type: 'libgrid', invert: false, source: 'groupable',
                desc: 'Groups the picked folders into views such as Movies, Music, and TV. The rest show in their own view.' }
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

    // fetchChannels
    // Reads the channels, as Jellyfin's library access page lists them.
    //
    // Output
    // [Promise]: The channels, or an empty list on failure.
    function fetchChannels() {
        try {
            return ApiClient.getJSON(ApiClient.getUrl('Channels'))
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
            fetchGroupingOptions(),
            fetchChannels()
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
            allChannels = results[9] || [];
            if (!fullConfig.Groups) fullConfig.Groups = [];

            renderSections();
            ensureMemberSelector();
            populateDropdown();
            if (tracker) tracker.markClean();
            if (saveBar) saveBar.setDirty(false);
            renderUserCards();
        }).catch(function () {
            if (saveBar) saveBar.setState('failed', 'Could not load the groups. Reload the page.');
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
                + '<span class="base-collapsible-icon"></span></button>'
                + '<div id="' + id + '" class="base-collapsible-content collapsed">'
                + '<div class="um-rows" data-label="' + esc(section.title) + '">';

            section.perms.forEach(function (p) {
                html += renderPermRow(p, store);
            });

            html += '</div></div></div>';
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
        mountRowLists(view.querySelector('#permissionSections'));
        mountRowLists(view.querySelector('#configSections'));
        bindPermRows();
    }

    // mountRowLists
    // Puts the rows of each um-rows block in a Base List, so every row is a bordered row with a divider between rows.
    //
    // Inputs
    // root [HTMLElement]: Where to look for um-rows blocks.
    function mountRowLists(root) {
        root.querySelectorAll('.um-rows').forEach(function (host) {
            if (host.getAttribute('data-mounted') === '1') return;
            host.setAttribute('data-mounted', '1');
            var rows = Array.prototype.slice.call(host.children);
            var list = createList({
                items: rows,
                render: function (row) { return row; },
                layout: 'rows',
                label: host.getAttribute('data-label') || null,
                emptyText: null
            });
            host.appendChild(list.element);
            if (root === view.querySelector('#permissionSections') || root === view.querySelector('#configSections')) {
                rowLists.push(list);
            }
        });
    }

    // setRowShown
    // Shows or hides one setting row, with the List cell that holds it.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
    // shown [boolean]: Whether it shows.
    function setRowShown(row, shown) {
        var cell = row.closest('.base-list-item') || row;
        cell.classList.toggle('hidden', !shown);
    }

    // mountInheritFields
    // Puts each row's lead, its checkbox or its label, in an Inherit Field whose pills read Member and
    // Group. The row's controls keep the group's own values, so the field handle reads and writes nothing.
    // While the row is Member, the Inherit Field dims the lead and disables its checkbox, and the controls
    // under the lead hide.
    function mountInheritFields() {
        view.querySelectorAll('.perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var lead = row.querySelector('.perm-lead');
            inheritHandles[key] = createInheritField({
                field: { element: lead, getValue: function () { return null; }, setValue: function () {} },
                inheritText: MEMBER_OWN,
                overrideText: GROUP_SETS,
                label: 'Who sets ' + row.getAttribute('data-label'),
                onChange: function () {
                    refreshRow(row);
                    refreshBadges();
                    if (key === 'LibraryAccess') applyAccessFilter();
                    checkDirty();
                }
            });
        });
    }

    // isManaged
    // Says whether a setting is on Group.
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

    // leadValue
    // Reads the group's own value of a row led by a checkbox.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
    //
    // Output
    // [boolean]: Whether the group's checkbox is on.
    function leadValue(row) {
        var input = row.querySelector('.perm-lead-input');
        return !!input && input.checked;
    }

    // setManaged
    // Sets whether a setting is on Group, and the group's own value of a checkbox lead.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
    // managed [boolean]: Whether the group's value applies.
    // value [boolean]: The checkbox lead's own value, or undefined for a label lead.
    function setManaged(row, managed, value) {
        var handle = inheritHandles[row.getAttribute('data-perm')];
        if (!handle) return;
        // The Inherit Field disables the lead's checkbox while inheriting, so it turns Group on first and
        // the checkbox takes its value while enabled.
        handle.setValue({ inherit: false });
        var input = row.querySelector('.perm-lead-input');
        if (input && value !== undefined) input.checked = value === true;
        if (!managed) handle.setValue({ inherit: true });
        refreshRow(row);
    }

    // refreshRow
    // Shows a row's controls only while it is Group, and an access list only while its "all" checkbox is off.
    //
    // Inputs
    // row [HTMLElement]: The setting row.
    function refreshRow(row) {
        var body = row.querySelector('.perm-body');
        if (!body) return;
        var managed = isManaged(row);
        var all = ALL_TYPES[row.getAttribute('data-type')] ? leadValue(row) : false;
        body.classList.toggle('hidden', !managed || all);
    }

    // refreshBadges
    // Counts the rows on Group in each section, and shows the count in the section header.
    function refreshBadges() {
        view.querySelectorAll('#permissionSections .base-collapsible-section, #configSections .base-collapsible-section').forEach(function (section) {
            var count = 0;
            section.querySelectorAll('.perm-row').forEach(function (row) { if (isManaged(row)) count++; });
            setCollapsibleBadge(section.querySelector('.base-collapsible-header'),
                count ? { label: count + ' Set by Group', color: 'blue' } : null);
        });
    }

    // destroyRowHandles
    // Tears down the Base components of the last draw.
    function destroyRowHandles() {
        Object.keys(inheritHandles).forEach(function (key) { inheritHandles[key].destroy(); });
        inheritHandles = {};
        Object.keys(rowHandles).forEach(function (key) { rowHandles[key].destroy(); });
        rowHandles = {};
        rowLists.forEach(function (list) { list.destroy(); });
        rowLists = [];
        if (sessionRules) {
            sessionRules.destroy();
            if (sessionRules.element.parentNode) sessionRules.element.parentNode.removeChild(sessionRules.element);
            sessionRules = null;
        }
    }

    // LIBRARY_TYPES
    // Names a library's collection type as Jellyfin's library page does.
    var LIBRARY_TYPES = {
        movies: 'Movies', tvshows: 'Shows', music: 'Music', musicvideos: 'Music Videos', books: 'Books',
        homevideos: 'Home Videos and Photos', boxsets: 'Collections', playlists: 'Playlists', mixed: 'Mixed Content'
    };

    // libraryRow
    // Draws one library in a library list. The sublabel names its type and its first folder.
    //
    // Inputs
    // l [object]: The library from getVirtualFolders, or a view with Id and Name.
    //
    // Output
    // [object]: The row's { label, sublabel }.
    function libraryRow(l) {
        var id = libraryId(l);
        var lib = allLibraries.find(function (x) { return x.ItemId === id; }) || l;
        var type = LIBRARY_TYPES[lib.CollectionType] || (lib.CollectionType ? 'Mixed Content' : '');
        var path = (lib.Locations && lib.Locations[0]) || '';
        return { label: l.Name || l.name || id, sublabel: [type, path].filter(Boolean).join(' · ') };
    }

    // libraryId
    // Reads the id of a library or a view.
    //
    // Inputs
    // l [object]: The library or view.
    //
    // Output
    // [string]: The id.
    function libraryId(l) { return l.ItemId || l.Id || l.id; }

    // channelRow
    // Draws one channel in the channel list. The sublabel names its type, so a channel reads apart from a library.
    //
    // Inputs
    // c [object]: The channel from the Channels API.
    //
    // Output
    // [object]: The row's { label, sublabel }.
    function channelRow(c) {
        return { label: c.Name || c.Id, sublabel: 'Channel' + (c.ChannelType ? ' · ' + c.ChannelType : '') };
    }

    // pickList
    // Builds a Checkbox List for an access row or a home screen row.
    //
    // Inputs
    // opts [object]: The Checkbox List options this list adds.
    //
    // Output
    // [object]: The Checkbox List handle.
    function pickList(opts) {
        return createCheckboxList(Object.assign({ maxHeight: 320, onChange: checkDirty }, opts));
    }

    // mountRowComponents
    // Mounts the Base component each row holds: a Checkbox List in the access and home screen library rows, a Tag
    // Input in each tags row, an Access Schedule in the schedule row, and an Ordered List in the library order row.
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
            } else if (type === 'library' || type === 'deletion') {
                handle = pickList({
                    items: allLibraries, idKey: 'ItemId', searchKeys: ['Name'],
                    placeholder: 'Search libraries', emptyMessage: 'No libraries found.', renderRow: libraryRow
                });
            } else if (type === 'channels') {
                handle = pickList({
                    items: allChannels, idKey: 'Id', searchKeys: ['Name'],
                    placeholder: 'Search channels', emptyMessage: 'No channels found.', renderRow: channelRow
                });
            } else if (type === 'devices') {
                handle = pickList({
                    items: allDevices, idKey: 'Id', searchKeys: ['CustomName', 'Name', 'AppName', 'LastUserName', 'Id'],
                    placeholder: 'Search devices', emptyMessage: 'No devices found.', renderRow: deviceRow
                });
            } else if (type === 'libgrid') {
                var source = row.getAttribute('data-source');
                handle = pickList({
                    fetchItems: function () { return homeLibraries(source); },
                    idKey: libraryId, searchKeys: ['Name', 'name'],
                    placeholder: 'Search libraries', emptyMessage: 'No libraries found.', renderRow: libraryRow
                });
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

    // homeLibraries
    // Lists the libraries a home screen row offers, leaving out the ones Library Access hides from members.
    //
    // Inputs
    // source [string]: groupable for the folders Jellyfin can group, or empty for every library.
    //
    // Output
    // [Array]: The libraries or folders.
    function homeLibraries(source) {
        var access = accessibleLibraryIds();
        var items = source === 'groupable' ? allGroupable : allLibraries;
        return items.filter(function (v) { return access === null || access[libraryId(v)] === true; });
    }

    // inItemOrder
    // Puts picked ids in the order their items are listed, dropping ids no item has.
    //
    // Inputs
    // ids [Array]: The picked ids.
    // items [Array]: The listed items.
    // idOf [function]: Reads an item's id.
    //
    // Output
    // [Array]: The ids, in item order.
    function inItemOrder(ids, items, idOf) {
        var picked = {};
        (ids || []).forEach(function (id) { picked[String(id)] = true; });
        return items.map(idOf).filter(function (id) { return picked[String(id)] === true; });
    }

    // deviceRow
    // Draws one device in the device list. The sublabel names the app and the last user, or the device id when no one
    // has signed in, so two devices with the same name can be told apart.
    //
    // Inputs
    // d [object]: The device from the Devices API.
    //
    // Output
    // [object]: The row's { label, sublabel }.
    function deviceRow(d) {
        var app = [d.AppName, d.AppVersion].filter(Boolean).join(' ');
        var who = d.LastUserName ? 'Last used by ' + d.LastUserName : 'ID ' + d.Id;
        return {
            label: d.CustomName || d.Name || d.AppName || d.Id,
            sublabel: [app, who].filter(Boolean).join(' · ')
        };
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

    // selectHtml
    // Builds a select in its own selectContainer, as Jellyfin's emby-select needs for its arrow.
    //
    // Inputs
    // id [string]: The select's id.
    // cls [string]: Its classes.
    // options [string]: Its options' HTML.
    // labelledBy [string]: The id of the element that names it.
    //
    // Output
    // [string]: The select's HTML.
    function selectHtml(id, cls, options, labelledBy) {
        return '<div class="selectContainer"><select is="emby-select" id="' + id + '" class="' + cls + '" aria-labelledby="'
            + labelledBy + '">' + options + '</select></div>';
    }

    // renderPermBody
    // Builds the controls under a row's lead, which show while the row is Group.
    //
    // Inputs
    // p [object]: The setting from PERMISSIONS or CONFIG.
    // controlId [string]: The id of the row's main control.
    // labelId [string]: The id of the row's label.
    //
    // Output
    // [string]: The controls' HTML, or empty for a checkbox row.
    function renderPermBody(p, controlId, labelId) {
        var esc = Shared.escapeHtml;
        var optionsOf = function (list) {
            return list.map(function (o) { return '<option value="' + esc(o[0]) + '">' + esc(o[1]) + '</option>'; }).join('');
        };
        if (p.type === 'bool') return '';
        if (ALL_TYPES[p.type] || p.type === 'tags' || p.type === 'schedule' || p.type === 'order' || p.type === 'libgrid') {
            return '<div class="perm-mount"></div>';
        }
        if (p.type === 'int' || p.type === 'bitrate') {
            return '<div class="inputContainer"><input is="emby-input" type="number" id="' + controlId + '" class="perm-value" min="'
                + (p.min != null ? p.min : 0) + '"' + (p.type === 'bitrate' ? ' step="0.1"' : '') + ' aria-labelledby="' + labelId + '" /></div>';
        }
        if (p.type === 'enum') return selectHtml(controlId, 'perm-value', optionsOf(p.options || []), labelId);
        if (p.type === 'rating') {
            var ropts = '<option value=""></option>';
            parentalRatings.forEach(function (r, i) {
                ropts += '<option value="' + i + '">' + esc(ratingName(r)) + '</option>';
            });
            return selectHtml(controlId, 'perm-value', ropts, labelId);
        }
        if (p.type === 'unrated') {
            return '<div class="base-check-grid perm-unrated-grid">' + UNRATED_ITEMS.map(function (u) {
                return '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-unrated" value="'
                    + esc(u[0]) + '" /><span class="checkboxLabel">' + esc(u[1]) + '</span></label>';
            }).join('') + '</div>';
        }
        if (p.type === 'submode') {
            return selectHtml(controlId, 'perm-value', optionsOf(SUBTITLE_MODES), labelId)
                + '<div class="fieldDescription perm-submode-help"></div>';
        }
        if (p.type === 'culture') return selectHtml(controlId, 'perm-value', cultureOptions(), labelId);
        if (p.type === 'castreceiver') {
            return selectHtml(controlId, 'perm-value', allCastReceivers.map(function (r) {
                return '<option value="' + esc(r.Id || r.id) + '">' + esc(r.Name || r.name || r.Id) + '</option>';
            }).join(''), labelId);
        }
        if (p.type === 'homesections') {
            var secOpts = optionsOf(HOME_SECTIONS);
            var fields = '';
            for (var hi = 0; hi < HOME_SECTION_COUNT; hi++) {
                fields += '<div class="selectContainer">'
                    + '<label class="selectLabel" for="permHomeSection' + hi + '">Home screen section ' + (hi + 1) + '</label>'
                    + '<select is="emby-select" id="permHomeSection' + hi + '" class="perm-homesection" data-index="' + hi + '">' + secOpts + '</select>'
                    + '</div>';
            }
            return fields;
        }
        return '';
    }

    // renderPermRow
    // Builds one setting row. The lead is the setting's checkbox, or its label when the setting has other controls,
    // and the Inherit Field puts the Member and Group pills beside it. The controls follow, then the
    // description.
    //
    // Inputs
    // p [object]: The setting from PERMISSIONS or CONFIG.
    // store [string]: perm or config.
    //
    // Output
    // [string]: The row's HTML.
    function renderPermRow(p, store) {
        var esc = Shared.escapeHtml;
        var controlId = 'perm' + store + p.key;
        var labelId = controlId + 'Label';
        var lead = (p.type === 'bool' || ALL_TYPES[p.type])
            ? '<div class="um-setting-check perm-lead"><label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-lead-input" />'
                + '<span class="checkboxLabel">' + esc(p.label) + '</span></label></div>'
            : '<span class="um-setting-label perm-lead" id="' + labelId + '">' + esc(p.label) + '</span>';
        var body = renderPermBody(p, controlId, labelId);

        var attrs = ' data-store="' + esc(store) + '" data-perm="' + esc(p.key) + '" data-type="' + esc(p.type) + '"'
            + ' data-label="' + esc(p.label) + '"'
            + (p.type === 'libgrid' ? ' data-invert="' + (p.invert ? '1' : '0') + '"' : '')
            + (p.source ? ' data-source="' + esc(p.source) + '"' : '')
            + (p.def ? ' data-default="1"' : '');

        return '<div class="um-setting perm-row"' + attrs + '>'
            + lead
            + (body ? '<div class="um-setting-body perm-body">' + body + '</div>' : '')
            + (p.desc ? '<div class="fieldDescription">' + esc(p.desc) + '</div>' : '')
            + '</div>';
    }

    // bindPermRows
    // Wires the controls inside each row.
    function bindPermRows() {
        view.querySelectorAll('.perm-row').forEach(function (row) {
            var type = row.getAttribute('data-type');

            if (ALL_TYPES[type]) {
                row.querySelector('.perm-lead-input').addEventListener('change', function () {
                    refreshRow(row);
                    if (type === 'library') applyAccessFilter();
                });
            }

            // The home screen library lists follow Library Access, so they are filtered again on each change.
            if (type === 'library') {
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
        if (tracker && !loading && saveBar) saveBar.setDirty(tracker.check());
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
    function loadConfigRow(row, cfg, key, type) {
        var managed = cfg['Manage' + key] === true;

        if (type === 'order') {
            // Saved views go first in their saved order, and the rest follow in the default order.
            var views = orderSource();
            var byId = {};
            views.forEach(function (v) { byId[v.id] = v; });
            var front = (cfg.OrderedViews || []).filter(function (id) { return byId[id]; }).map(function (id) { return byId[id]; });
            orderState = front.concat(views.filter(function (v) { return front.indexOf(v) === -1; }));
        } else if (type === 'libgrid') {
            // A row lists libraries as shown, so an exclude list is turned around into the libraries that show.
            var invert = row.getAttribute('data-invert') === '1';
            var stored = (cfg[key] || []).map(String);
            var items = row.getAttribute('data-source') === 'groupable' ? allGroupable : allLibraries;
            var shown = items.map(libraryId).filter(function (id) {
                var listed = stored.indexOf(String(id)) !== -1;
                return invert ? !listed : listed;
            });
            if (rowHandles[key]) rowHandles[key].setValue(shown);
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
            var stored2 = cfg[key];
            setManaged(row, managed, (stored2 === true || stored2 === false)
                ? stored2 === true
                : row.getAttribute('data-default') === '1');
            return;
        }
        setManaged(row, managed);
    }

    // collectConfigRow
    // Reads one preference row into the group's configuration.
    //
    // Inputs
    // row [HTMLElement]: The row.
    // cfg [object]: The group's configuration.
    // key [string]: The setting.
    // type [string]: The row's control type.
    function collectConfigRow(row, cfg, key, type) {
        cfg['Manage' + key] = isManaged(row);

        if (type === 'order') {
            // Only the libraries members can see are shown, so only those are saved in the order.
            var order = rowHandles[key];
            cfg.OrderedViews = order ? order.getItems().map(function (v) { return v.id; }) : [];
        } else if (type === 'libgrid') {
            // Only the libraries members can see are listed, so only those are saved.
            var invert = row.getAttribute('data-invert') === '1';
            var checked = {};
            (rowHandles[key] ? rowHandles[key].getValue() : []).forEach(function (id) { checked[String(id)] = true; });
            cfg[key] = homeLibraries(row.getAttribute('data-source')).map(libraryId).filter(function (id) {
                return invert ? !checked[String(id)] : checked[String(id)] === true;
            });
        } else if (type === 'homesections') {
            var secs = [];
            row.querySelectorAll('.perm-homesection').forEach(function (sel) { secs.push(sel.value); });
            cfg.HomeSections = secs;
        } else if (type === 'submode' || type === 'culture' || type === 'castreceiver') {
            cfg[key] = row.querySelector('.perm-value').value;
        } else {
            cfg[key] = leadValue(row);
        }
    }

    // accessibleLibraryIds
    // Reads the libraries members can see from the Library Access row.
    //
    // Output
    // [object]: A set of library ids, or null when every library is visible.
    function accessibleLibraryIds() {
        var row = view.querySelector('.perm-row[data-perm="LibraryAccess"]');
        if (!row || !inheritHandles.LibraryAccess || !rowHandles.LibraryAccess) return null;
        if (!isManaged(row) || leadValue(row)) return null;
        var set = {};
        rowHandles.LibraryAccess.getValue().forEach(function (id) { set[id] = true; });
        return set;
    }

    // applyAccessFilter
    // Hides the home screen libraries members cannot see, so an admin cannot order or pick a hidden library.
    function applyAccessFilter() {
        var access = accessibleLibraryIds();
        var accessible = function (id) { return access === null || access[id] === true; };
        // Derived views such as Live TV and Collections are not libraries, so they always stay.
        var libIds = {};
        allLibraries.forEach(function (l) { libIds[l.ItemId] = true; });

        view.querySelectorAll('#configSections .perm-row').forEach(function (row) {
            var type = row.getAttribute('data-type');
            var handle = rowHandles[row.getAttribute('data-perm')];
            if (!handle) return;
            if (type === 'order') {
                handle.setItems(orderState.filter(function (v) { return libIds[v.id] !== true || accessible(v.id); }));
            } else if (type === 'libgrid') {
                handle.refresh();
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

            if (row.getAttribute('data-store') === 'config') {
                loadConfigRow(row, cfg, key, type);
                return;
            }

            var managed = perms['Manage' + key] === true;
            var lead;
            if (type === 'library') {
                lead = perms.EnableAllFolders !== false;
                if (rowHandles[key]) rowHandles[key].setValue(perms.EnabledFolders || []);
            } else if (type === 'channels') {
                lead = perms.EnableAllChannels !== false;
                if (rowHandles[key]) rowHandles[key].setValue(perms.EnabledChannels || []);
            } else if (type === 'deletion') {
                lead = perms.EnableContentDeletion === true;
                if (rowHandles[key]) rowHandles[key].setValue(perms.EnableContentDeletionFromFolders || []);
            } else if (type === 'devices') {
                lead = perms.EnableAllDevices !== false;
                if (rowHandles[key]) rowHandles[key].setValue(perms.EnabledDevices || []);
            } else if (type === 'rating') {
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
                var blocked = perms.BlockUnratedItems || [];
                row.querySelectorAll('.perm-unrated').forEach(function (cb) { cb.checked = blocked.indexOf(cb.value) !== -1; });
            } else if (type === 'tags') {
                if (rowHandles[key]) rowHandles[key].setValue(perms[key] || []);
            } else if (type === 'schedule') {
                if (rowHandles[key]) rowHandles[key].setValue(perms.AccessSchedules || []);
            } else if (type === 'bitrate') {
                row.querySelector('.perm-value').value = (perms.RemoteClientBitrateLimit || 0) / 1000000 || '';
            } else if (type === 'bool') {
                lead = perms[key] === true;
            } else if (type === 'int') {
                row.querySelector('.perm-value').value = perms[key] || 0;
            } else {
                var val = row.querySelector('.perm-value');
                val.value = perms[key] || (val.options[0] && val.options[0].value);
            }

            setManaged(row, managed, lead);
        });

        refreshBadges();
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

    // showRuleRows
    // Shows the rows of an Account Rules section while its first checkbox is on, and marks the section header On.
    //
    // Inputs
    // checkboxId [string]: The id of the checkbox that turns the rule on.
    // rowClass [string]: The class of the rows that depend on it.
    function showRuleRows(checkboxId, rowClass) {
        var box = view.querySelector('#' + checkboxId);
        if (!box) return;
        var on = box.checked;
        view.querySelectorAll('.' + rowClass).forEach(function (row) { setRowShown(row, on); });
        var section = box.closest('.base-collapsible-section');
        if (section) setCollapsibleBadge(section.querySelector('.base-collapsible-header'), on ? { label: 'On', color: 'blue' } : null);
    }

    // updatePwEnabledState
    // Shows the password rows while password requirements are on.
    function updatePwEnabledState() { showRuleRows('grpPwEnabled', 'um-pw-field'); }

    // updateExpiryEnabledState
    // Shows the expiry rows while expiry is on.
    function updateExpiryEnabledState() { showRuleRows('grpExpiryEnabled', 'um-expiry-field'); }

    // updateInactiveEnabledState
    // Shows the inactivity row while inactivity is on.
    function updateInactiveEnabledState() { showRuleRows('grpInactiveEnabled', 'um-inactive-field'); }

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
    function updateSessionEnabledState() { showRuleRows('grpSessionEnabled', 'um-session-field'); }

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

            if (row.getAttribute('data-store') === 'config') {
                collectConfigRow(row, cfg, key, type);
                return;
            }

            perms['Manage' + key] = isManaged(row);
            var picked = rowHandles[key] ? rowHandles[key].getValue() : [];
            if (type === 'library') {
                perms.EnableAllFolders = leadValue(row);
                perms.EnabledFolders = inItemOrder(picked, allLibraries, libraryId);
            } else if (type === 'channels') {
                perms.EnableAllChannels = leadValue(row);
                // With no channels to list, the saved channels are kept as they are. A group that never saved a
                // channel list and picks none is left without one, which reads as the same empty list.
                if (allChannels.length && (perms.EnabledChannels !== undefined || picked.length)) perms.EnabledChannels = inItemOrder(picked, allChannels, function (c) { return c.Id; });
            } else if (type === 'deletion') {
                perms.EnableContentDeletion = leadValue(row);
                perms.EnableContentDeletionFromFolders = inItemOrder(picked, allLibraries, libraryId);
            } else if (type === 'devices') {
                perms.EnableAllDevices = leadValue(row);
                perms.EnabledDevices = picked;
            } else if (type === 'rating') {
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
                var items = [];
                row.querySelectorAll('.perm-unrated:checked').forEach(function (cb) { items.push(cb.value); });
                perms.BlockUnratedItems = items;
            } else if (type === 'tags' || type === 'schedule') {
                perms[key] = picked;
            } else if (type === 'bitrate') {
                var mbps = parseFloat(row.querySelector('.perm-value').value);
                perms.RemoteClientBitrateLimit = isNaN(mbps) || mbps < 0 ? 0 : Math.round(mbps * 1000000);
            } else if (type === 'bool') {
                perms[key] = leadValue(row);
            } else if (type === 'int') {
                perms[key] = parseInt(row.querySelector('.perm-value').value, 10) || 0;
            } else {
                perms[key] = row.querySelector('.perm-value').value;
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
    //
    // Output
    // [Promise]: Settles when the groups are saved and applied, and rejects when either fails.
    function save() {
        return persistGroups().then(function () {
            return Shared.apiRequest('Apply', 'POST');
        }).then(function () {
            if (tracker) tracker.markClean();
            renderUserCards();
        }).catch(function () {
            throw new Error('Could not save the groups.');
        });
    }

    // confirmSave
    // Runs before the Save Bar's own click. It reads the open group, asks first when the save would delete members,
    // and then saves through the bar.
    //
    // Inputs
    // e [Event]: The click on the Save button.
    function confirmSave(e) {
        e.stopPropagation();
        if (currentGroupId) collectCurrentGroup();
        confirmDeleteExpiry().then(function (ok) {
            if (ok) saveBar.save();
        });
    }

    // bind
    // Wires the page once and starts the Change Tracker.
    //
    // Inputs
    // shared [object]: The helper bag from createPage.
    function bind(shared) {
        tracker = shared.createChangeTracker({ serialize: serializeGroups });
        saveBar = createSaveBar({ fill: true, savedText: 'Saved and applied to members.', onSave: save });
        // A capture listener on the bar runs before the button's own, so the confirm comes before Saving shows.
        saveBar.element.addEventListener('click', function (e) {
            if (e.target.closest('button') === saveBar.button) confirmSave(e);
        }, true);
        view.querySelector('#groupSaveBar').appendChild(saveBar.element);
        mountRowLists(view.querySelector('#groupEditor'));
        view.querySelector('#selectGroup').addEventListener('change', function () {
            collectCurrentGroup();
            currentGroupId = this.value;
            loadCurrentGroup();
        });
        view.querySelector('#btnNewGroup').addEventListener('click', newGroup);
        view.querySelector('#btnRenameGroup').addEventListener('click', renameGroup);
        view.querySelector('#btnDeleteGroup').addEventListener('click', deleteGroup);
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
