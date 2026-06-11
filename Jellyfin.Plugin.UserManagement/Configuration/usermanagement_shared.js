import { createShared as base, generateGuid, createUserMultiSelector } from '/web/configurationpage?name=usermanagement_jpkribs_shared.js';

export { generateGuid, createUserMultiSelector };

export function getTabs() {
    return [
        { href: 'configurationpage?name=usermanagement_user', name: 'Groups' },
        { href: 'configurationpage?name=usermanagement_invites', name: 'Invites' },
        { href: 'configurationpage?name=usermanagement_resets', name: 'Resets' }
    ];
}

export function createShared(view) {
    return base(view, '670167bd-e7f8-4549-98e2-5ab2e11bc89f', 'UserManagement');
}
