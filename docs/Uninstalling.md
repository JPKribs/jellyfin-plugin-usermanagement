# Uninstalling

!!! danger
    **If a group enforces password requirements, turn that enforcement off (or delete the group) and let the sync run before uninstalling the plugin.**

Members of such groups are switched onto this plugin's authentication provider while enrolled, and disabling the enforcement switches every member back to the provider they had before. If the plugin is removed while users are still enrolled, Jellyfin assigns those users an invalid authentication provider and **they cannot sign in** until an administrator reassigns the authentication provider on each user's profile page.

## Safe Order

1. Open the **Groups** tab and turn **Enforce password requirements** off on every group that has it (or delete those groups).
2. Save. The save runs an apply, which reverts every enrolled member to the provider they had before.
3. Confirm on **Dashboard → Activity** that a **Removed from group password rules** entry exists for each member.
4. Uninstall the plugin.

This only affects members of groups with password requirements enabled. Users outside those groups and administrators are unaffected.

## Recovery

If the plugin is already gone and users cannot sign in, the recovery is per user: **Dashboard → Users →** the user **→ Authentication provider**, then set it back to **Default**.
