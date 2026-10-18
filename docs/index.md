# User Management

![User Management](assets/logo.png){ width="160" }

User Management is a Jellyfin plugin for managing Jellyfin users via groups. It manages:

* **[Permissions](Groups.md)**: Enforce the same permissions across multiple users using groups.
* **[Password Validation](Passwords.md)**: Enforce password length and complexity for all users in a group.
* **[User Expiry](Expiration.md)**: Disable or delete user accounts based on a group set date.
* **[User Inactivity](Inactivity.md)**: Disable inactive user accounts based on a group time limit.
* **[Session Cleanup](Sessions.md)**: Log out devices that have gone too long without checking back into the server.
* **[Invite Links](Invites.md)**: Create an invite link to create new accounts on your server and automatically assign them a group.

## How It Works

User Management uses groups to assign permissions and rules to all users who are a part of this group. New users can be onboarded from an invite link and have group permissions assigned to them on creation. On update, this plugin finds all users in these groups and updates their `UserDto.userConfiguration` using all overridden permissions and settings. Anything not found in the base `UserDto` is stored and referenced from the plugin storage.

Next: [Installation](Installation.md).
