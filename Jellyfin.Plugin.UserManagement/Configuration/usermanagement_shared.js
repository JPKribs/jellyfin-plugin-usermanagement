// usermanagement_shared.js
// Wraps the Base shared bundle for the User Management pages. It re-exports the Base builders the pages use
// and adds createPage(view, tabIndex, opts), which fills in the plugin id, API prefix, and tab list.

// The specifier is relative so it resolves against this module's own URL, and a server hosted under
// a base URL still loads the bundle.
import {
    createConfigPage,
    generateGuid,
    createUserMultiSelector,
    createAccessSchedule,
    createTagInput,
    createOrderedList,
    createKeyValueEditor,
    createCopyField,
    createRecordList,
    createDefinitionList,
    createInheritField,
    createRepeater,
    createChipSelect,
    createCheckboxList,
    createList,
    setCollapsibleBadge,
    openEditorDialog,
    confirmDialog,
    promptDialog,
    createSaveBar
} from './configurationpage?name=usermanagement_jpkribs_shared.js';

// Re-exports
// The Base builders, so a page takes everything from this one module.
export {
    generateGuid,
    createUserMultiSelector,
    createAccessSchedule,
    createTagInput,
    createOrderedList,
    createKeyValueEditor,
    createCopyField,
    createRecordList,
    createDefinitionList,
    createInheritField,
    createRepeater,
    createChipSelect,
    createCheckboxList,
    createList,
    setCollapsibleBadge,
    openEditorDialog,
    confirmDialog,
    promptDialog,
    createSaveBar
};

var PLUGIN_ID = '670167bd-e7f8-4549-98e2-5ab2e11bc89f';

// getTabs
// Lists the plugin's tabs in the order the tab bar shows them.
//
// Output
// [Array]: One { href, name } per tab.
export function getTabs() {
    return [
        { href: 'configurationpage?name=usermanagement_groups', name: 'Groups' },
        { href: 'configurationpage?name=usermanagement_invites', name: 'Invites' },
        { href: 'configurationpage?name=usermanagement_resets', name: 'Resets' }
    ];
}

// createPage
// Wires a tab page's lifecycle through Base's createConfigPage, with the plugin's id, API prefix, and tab bar filled in.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
// tabIndex [number]: The page's place in the tab bar.
// opts [object]: The createConfigPage options the page adds, such as bind, load, and isDirty.
//
// Output
// [object]: The page handle, whose shared is the helper bag.
export function createPage(view, tabIndex, opts) {
    return createConfigPage(view, Object.assign({
        pluginId: PLUGIN_ID,
        apiPrefix: 'UserManagement',
        tabsKey: 'usermanagement',
        tabIndex: tabIndex,
        tabs: getTabs()
    }, opts));
}
