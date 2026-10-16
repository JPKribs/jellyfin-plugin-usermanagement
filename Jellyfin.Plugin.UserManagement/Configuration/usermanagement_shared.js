import { createConfigPage, generateGuid, createUserMultiSelector, bindDialog, confirmDialog } from './configurationpage?name=usermanagement_jpkribs_shared.js';

export { generateGuid, createUserMultiSelector, bindDialog, confirmDialog };

var PLUGIN_ID = '670167bd-e7f8-4549-98e2-5ab2e11bc89f';

export function getTabs() {
    return [
        { href: 'configurationpage?name=usermanagement_groups', name: 'Groups' },
        { href: 'configurationpage?name=usermanagement_invites', name: 'Invites' },
        { href: 'configurationpage?name=usermanagement_resets', name: 'Resets' }
    ];
}

// Wires a tab page's lifecycle (tabs, one time bind, load on every viewshow) with the plugin's id,
// API prefix, and tab bar filled in.
export function createPage(view, tabIndex, opts) {
    return createConfigPage(view, Object.assign({
        pluginId: PLUGIN_ID,
        apiPrefix: 'UserManagement',
        tabsKey: 'usermanagement',
        tabIndex: tabIndex,
        tabs: getTabs()
    }, opts));
}
