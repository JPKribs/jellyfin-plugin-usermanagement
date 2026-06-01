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
    var serverBitrate = 0; // server-wide RemoteClientBitrateLimit in bps (0 = unlimited)
    var _snapshot = null;  // JSON of fullConfig at last save/load, for unsaved-changes detection
    var _dirty = false;

    // Permission schema: drives both the rendered rows and the load/collect mapping.
    var PERMISSIONS = [
        { title: 'Administration', perms: [
            { key: 'IsHidden', label: 'Hidden from login screen', type: 'bool' },
            { key: 'IsDisabled', label: 'Disabled', type: 'bool' },
            { key: 'EnableCollectionManagement', label: 'Allow collection management', type: 'bool' },
            { key: 'EnableSubtitleManagement', label: 'Allow subtitle management', type: 'bool' },
            { key: 'EnableLyricManagement', label: 'Allow lyric management', type: 'bool' }
        ] },
        { title: 'Library Access', perms: [
            { key: 'LibraryAccess', label: 'Library access', type: 'library' },
            { key: 'ChannelAccess', label: 'Channel access', type: 'channels' }
        ] },
        { title: 'Playback & Transcoding', perms: [
            { key: 'EnableMediaPlayback', label: 'Allow media playback', type: 'bool' },
            { key: 'EnableAudioPlaybackTranscoding', label: 'Allow audio transcoding', type: 'bool' },
            { key: 'EnableVideoPlaybackTranscoding', label: 'Allow video transcoding', type: 'bool' },
            { key: 'EnablePlaybackRemuxing', label: 'Allow remuxing', type: 'bool' },
            { key: 'ForceRemoteSourceTranscoding', label: 'Force remote-source transcoding', type: 'bool' },
            { key: 'EnableMediaConversion', label: 'Allow media conversion', type: 'bool' },
            { key: 'EnableSyncTranscoding', label: 'Allow sync transcoding', type: 'bool' }
        ] },
        { title: 'Remote & Sessions', perms: [
            { key: 'EnableRemoteAccess', label: 'Allow remote access', type: 'bool' },
            { key: 'EnableRemoteControlOfOtherUsers', label: 'Allow remote control of other users', type: 'bool' },
            { key: 'EnableSharedDeviceControl', label: 'Allow shared device control', type: 'bool' },
            { key: 'MaxActiveSessions', label: 'Max active sessions (0 = unlimited)', type: 'int' },
            { key: 'RemoteClientBitrateLimit', label: 'Remote bitrate limit (bps, 0 = unlimited)', type: 'int' }
        ] },
        { title: 'Downloads & Deletion', perms: [
            { key: 'EnableContentDownloading', label: 'Allow downloads', type: 'bool' },
            { key: 'EnableContentDeletion', label: 'Allow content deletion', type: 'deletion' }
        ] },
        { title: 'Live TV', perms: [
            { key: 'EnableLiveTvAccess', label: 'Allow Live TV access', type: 'bool' },
            { key: 'EnableLiveTvManagement', label: 'Allow Live TV management', type: 'bool' }
        ] },
        { title: 'Other', perms: [
            { key: 'EnableUserPreferenceAccess', label: 'Allow user preference access', type: 'bool' },
            { key: 'EnablePublicSharing', label: 'Allow public sharing', type: 'bool' },
            { key: 'SyncPlayAccess', label: 'SyncPlay access', type: 'enum', options: [
                ['CreateAndJoinGroups', 'Create and join groups'],
                ['JoinGroups', 'Join groups'],
                ['None', 'None']
            ] }
        ] }
    ];

    // ── Loading ──────────────────────────────────────────────

    function loadAll() {
        Dashboard.showLoadingMsg();
        Promise.all([
            ApiClient.getPluginConfiguration(pluginId),
            ApiClient.getUsers(),
            ApiClient.getVirtualFolders(),
            ApiClient.getServerConfiguration()
        ]).then(function (results) {
            fullConfig = results[0];
            allUsers = results[1] || [];
            allLibraries = results[2] || [];
            serverBitrate = (results[3] && results[3].RemoteClientBitrateLimit) || 0;
            if (!fullConfig.Groups) fullConfig.Groups = [];

            renderSections();
            populateDropdown();
            snapshot();
            Dashboard.hideLoadingMsg();
        }).catch(function (err) {
            console.error('Failed to load groups', err);
            Dashboard.hideLoadingMsg();
        });
    }

    // ── Section rendering (schema-driven) ────────────────────

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

    function folderGrid(gridClass, itemClass) {
        var esc = Shared.escapeHtml;
        return '<div class="um-folder-grid ' + gridClass + '">'
            + allLibraries.map(function (lib) {
                return '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="' + itemClass + '" value="'
                    + esc(lib.ItemId) + '" /><span class="checkboxLabel">' + esc(lib.Name) + '</span></label>';
            }).join('')
            + '</div>';
    }

    function bitrateHintText() {
        if (!serverBitrate || serverBitrate <= 0) {
            return '0 = use the server limit (currently unlimited).';
        }
        var mbps = Math.round((serverBitrate / 1000000) * 10) / 10;
        return '0 = use the server limit (currently ' + mbps + ' Mbps).';
    }

    function renderPermRow(p) {
        var esc = Shared.escapeHtml;
        var control = '';
        var desc = '';

        if (p.type === 'bool') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-value" />'
                + '<span class="checkboxLabel">Enabled</span></label>';
        } else if (p.type === 'int') {
            control = '<input is="emby-input" type="number" min="0" class="perm-value um-edit-input" />';
            if (p.key === 'RemoteClientBitrateLimit') {
                desc = '<div class="fieldDescription" id="bitrateHint">' + esc(bitrateHintText()) + '</div>';
            }
        } else if (p.type === 'enum') {
            var opts = (p.options || []).map(function (o) {
                return '<option value="' + esc(o[0]) + '">' + esc(o[1]) + '</option>';
            }).join('');
            control = '<select is="emby-select" class="perm-value emby-select-withcolor">' + opts + '</select>';
        } else if (p.type === 'library') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-allfolders" />'
                + '<span class="checkboxLabel">All libraries</span></label>'
                + folderGrid('perm-folders', 'perm-folder');
        } else if (p.type === 'deletion') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-alldelete" />'
                + '<span class="checkboxLabel">Allow deletion from all libraries</span></label>'
                + folderGrid('perm-delete-folders', 'perm-delete-folder');
        } else if (p.type === 'channels') {
            control = '<label class="emby-checkbox-label"><input type="checkbox" is="emby-checkbox" class="perm-allchannels" />'
                + '<span class="checkboxLabel">All channels</span></label>';
        }

        return '<div class="um-perm-row inherited" data-perm="' + esc(p.key) + '" data-type="' + esc(p.type) + '">'
            + '<label class="emby-checkbox-label um-perm-toggle" title="Override this permission for the group">'
            + '<input type="checkbox" is="emby-checkbox" class="perm-manage" />'
            + '<span class="checkboxLabel">Override</span></label>'
            + '<div class="um-perm-main">'
            + '<div class="um-perm-label">' + esc(p.label) + '</div>'
            + '<div class="um-perm-control">' + control + '</div>'
            + desc
            + '</div></div>';
    }

    function bindPermRows() {
        view.querySelectorAll('.um-perm-row').forEach(function (row) {
            var manage = row.querySelector('.perm-manage');
            manage.addEventListener('change', function () { updateRowInherited(row); });

            row.querySelectorAll('.perm-allfolders, .perm-alldelete').forEach(function (toggle) {
                toggle.addEventListener('change', function () { updateFolderVisibility(row); });
            });
        });
    }

    function updateRowInherited(row) {
        var managed = row.querySelector('.perm-manage').checked;
        row.classList.toggle('inherited', !managed);
        row.querySelectorAll('.perm-value, .perm-allfolders, .perm-folder, .perm-allchannels, .perm-alldelete, .perm-delete-folder')
            .forEach(function (el) { el.disabled = !managed; });
        if (managed) updateFolderVisibility(row);
    }

    function updateFolderVisibility(row) {
        var pairs = [
            ['.perm-allfolders', '.perm-folders'],
            ['.perm-alldelete', '.perm-delete-folders']
        ];
        pairs.forEach(function (pair) {
            var all = row.querySelector(pair[0]);
            var grid = row.querySelector(pair[1]);
            if (all && grid) {
                grid.style.display = all.checked ? 'none' : 'grid';
            }
        });
    }

    // ── Dropdown ─────────────────────────────────────────────

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
            opt.textContent = g.Name || 'Unnamed group';
            select.appendChild(opt);
        });

        var hasGroups = groups.length > 0;
        Shared.setVisible('groupEditor', hasGroups);
        Shared.setVisible('emptyState', !hasGroups);
        view.querySelector('#btnRenameGroup').classList.toggle('hidden', !hasGroups);
        view.querySelector('#btnDeleteGroup').classList.toggle('hidden', !hasGroups);

        if (!hasGroups) { currentGroupId = null; return; }

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

    // ── Unsaved-changes tracking ─────────────────────────────

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

    // Persist only the groups this page owns, re-fetching the latest config first so we never clobber
    // server-side state this page doesn't manage (Invites, the default group set in Settings, etc.).
    // If the configured default group was just deleted here, clear that dangling reference.
    function persistGroups() {
        return ApiClient.getPluginConfiguration(pluginId).then(function (latest) {
            latest.Groups = fullConfig.Groups;
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

    // ── Load / collect permissions ───────────────────────────

    function loadCurrentGroup() {
        var group = getCurrentGroup();
        if (!group) return;
        var perms = group.Permissions || {};

        view.querySelectorAll('.um-perm-row').forEach(function (row) {
            var key = row.getAttribute('data-perm');
            var type = row.getAttribute('data-type');
            var manage = row.querySelector('.perm-manage');

            if (type === 'library') {
                manage.checked = perms.ManageLibraryAccess === true;
                var allF = row.querySelector('.perm-allfolders');
                allF.checked = perms.EnableAllFolders !== false;
                var enabled = perms.EnabledFolders || [];
                row.querySelectorAll('.perm-folder').forEach(function (cb) {
                    cb.checked = enabled.indexOf(cb.value) !== -1;
                });
            } else if (type === 'channels') {
                manage.checked = perms.ManageChannelAccess === true;
                row.querySelector('.perm-allchannels').checked = perms.EnableAllChannels !== false;
            } else if (type === 'deletion') {
                manage.checked = perms.ManageEnableContentDeletion === true;
                row.querySelector('.perm-alldelete').checked = perms.EnableContentDeletion === true;
                var delFolders = perms.EnableContentDeletionFromFolders || [];
                row.querySelectorAll('.perm-delete-folder').forEach(function (cb) {
                    cb.checked = delFolders.indexOf(cb.value) !== -1;
                });
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

        renderMembers();
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
    }

    // ── Members (inline checklist) ───────────────────────────

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

            // Admins are exempt from group enforcement, and a user already in another group
            // must be removed there first — both are shown disabled with the reason.
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
            // One group at a time: drop from every group, then add to this one.
            fullConfig.Groups.forEach(function (g) {
                g.MemberIds = (g.MemberIds || []).filter(function (x) { return x !== userId; });
            });
            group.MemberIds.push(userId);
        } else {
            group.MemberIds = (group.MemberIds || []).filter(function (x) { return x !== userId; });
        }
        renderMembers(); // refresh the "in <group>" notes after a move
        checkDirty();
    }

    function filterMembers() {
        renderMembers();
    }

    // ── Input modal (name) ───────────────────────────────────

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

    // ── CRUD ─────────────────────────────────────────────────

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
            fullConfig.Groups = fullConfig.Groups.filter(function (g) { return g.Id !== group.Id; });
            currentGroupId = null;
            populateDropdown();
            checkDirty();
        });
    }

    // ── Save ─────────────────────────────────────────────────

    function save() {
        if (currentGroupId) collectCurrentGroup();
        Dashboard.showLoadingMsg();
        persistGroups().then(function () {
            // Push managed permissions onto members now.
            return Shared.apiRequest('Apply', 'POST');
        }).then(function () {
            Dashboard.hideLoadingMsg();
            snapshot();
            Shared.setStatus('groupStatus', 'Saved and applied to members.', false);
        }).catch(function (err) {
            console.error('Save failed', err);
            Dashboard.hideLoadingMsg();
            Shared.setStatus('groupStatus', 'Save failed.', true);
        });
    }

    // ── Event listeners ──────────────────────────────────────

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

        view.querySelector('#memberSearch').addEventListener('input', filterMembers);

        // Any edit inside the form flips the unsaved-changes indicator.
        var form = view.querySelector('#UserManagementGroupsForm');
        if (form) {
            form.addEventListener('change', checkDirty);
            form.addEventListener('input', checkDirty);
        }
    });
}
