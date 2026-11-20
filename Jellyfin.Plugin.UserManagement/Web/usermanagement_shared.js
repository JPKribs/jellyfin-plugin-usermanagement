// usermanagement_shared.js
// Wraps the Base shared bundle for the User Management pages. It re-exports the Base builders the pages use,
// holds the plugin's strings in the viewer's language, and adds createPage(view, tabIndex, opts), which fills in
// the plugin id, API prefix, and tab list.

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
    createQrCode,
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
    createSaveBar,
    createTranslations,
    loadTranslations,
    setTabs
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
    createQrCode,
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
var TABS_KEY = 'usermanagement';
var _strings = createTranslations({}, 'en-us');
var _loading = null;

// t
// Reads the plugin's strings in the viewer's language. Before they load, tr gives the key.
//
// Output
// [object]: { tr(key), format(key, ...values), apply(root), language() }.
export var t = {
    tr: function (key) { return _strings.tr(key); },
    format: function () { return _strings.format.apply(_strings, arguments); },
    apply: function (root) { _strings.apply(root); },
    language: function () { return _strings.language; }
};

// loadStrings
// Loads the plugin's strings once per page load, in the viewer's language.
//
// Output
// [Promise]: Settles with the strings handle. It never rejects.
export function loadStrings() {
    if (!_loading) {
        _loading = loadTranslations('UserManagement/Translations').then(function (handle) {
            _strings = handle;
            return handle;
        });
    }
    return _loading;
}

// withLanguage
// Adds the viewer's language to an API path, so a message the server returns reads in it.
//
// Inputs
// path [string]: The API path under UserManagement.
//
// Output
// [string]: The path with its language query.
export function withLanguage(path) {
    return path + (path.indexOf('?') === -1 ? '?' : '&') + 'language=' + encodeURIComponent(t.language());
}

// saveBarOptions
// Fills the Save Bar's text in the viewer's language.
//
// Inputs
// opts [object]: The createSaveBar options the page adds, such as onSave and savedText.
//
// Output
// [object]: The options with every text filled.
export function saveBarOptions(opts) {
    return Object.assign({
        saveText: t.tr('Save'),
        pendingText: t.tr('SaveBarUnsaved'),
        savingText: t.tr('SaveBarSaving'),
        savedText: t.tr('SaveBarSaved'),
        failedText: t.tr('SaveBarFailed'),
        invalidText: t.tr('SaveBarInvalid')
    }, opts);
}

// getTabs
// Lists the plugin's tabs in the order the tab bar shows them.
//
// Output
// [Array]: One { href, name } per tab.
export function getTabs() {
    return [
        { href: 'configurationpage?name=usermanagement_groups', name: t.tr('HeaderGroups') },
        { href: 'configurationpage?name=usermanagement_invites', name: t.tr('HeaderInvites') },
        { href: 'configurationpage?name=usermanagement_resets', name: t.tr('TabResets') }
    ];
}

// createPage
// Wires a tab page's lifecycle through Base's createConfigPage, with the plugin's id, API prefix, and tab bar filled in.
// The page's bind and load wait for the strings, and the page's marked markup is translated before bind runs.
//
// Inputs
// view [HTMLElement]: The page the dashboard passes to the module.
// tabIndex [number]: The page's place in the tab bar.
// opts [object]: The createConfigPage options the page adds, such as bind, load, and isDirty.
//
// Output
// [object]: The page handle, whose shared is the helper bag.
export function createPage(view, tabIndex, opts) {
    opts = opts || {};
    return createConfigPage(view, Object.assign({}, opts, {
        pluginId: PLUGIN_ID,
        apiPrefix: 'UserManagement',
        tabsKey: TABS_KEY,
        tabIndex: tabIndex,
        tabs: getTabs,
        bind: function (shared, page) {
            loadStrings().then(function () {
                t.apply(view);
                setTabs(TABS_KEY, tabIndex, getTabs);
                if (opts.isDirty) page.guardUnsaved(opts.isDirty, t.tr('MessageLeaveUnsaved'));
                if (opts.bind) opts.bind(shared, page);
            });
        },
        load: function (shared, page) {
            loadStrings().then(function () {
                if (opts.load) opts.load(shared, page);
            });
        }
    }));
}
