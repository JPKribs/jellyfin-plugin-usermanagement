# Invites

Create a shareable signup link tied to a group. Anyone with the link can create their own account on your server. All users that use the link are created using the group assigned to it. For added security, an optional PIN can be set, and the user will have to provide it to use the link. Additionally, you can set a rate limit on how many times the link can be used over a period of time to avoid spam.

New invites are made from the **Create Invite** section of the Invites tab.

![Create Invite form](screenshots/Create%20Invite.png)

## Group

Pick either a specific group or **Default group**. A specific group is fixed for the life of the link. **Default group** resolves at the moment the link is redeemed, so the invite follows whichever group is marked default on the Groups tab, even if that changes after the link is sent.

## Options

Each invite can also carry:

* **Name**: An admin name for the invite. It is only shown on the dashboard, so this field is for admin reference only.
* **Welcome message**: Shown to the invitee under the signup heading.
* **Resources**: Title and URL pairs presented as link buttons after the account is created, for example a request site or a getting started guide. Only absolute http(s) URLs are accepted.

## Managing Invites

Every invite is listed with its link, usage, group and expiration, along with buttons to copy, lock or delete it.

![Invite list](screenshots/Invite%20List.png)

**Manage Access** sets the public server URL used to build links and the rate limit shared by all invites.

![Manage Access settings](screenshots/Manage%20Access.png)

## Signup Page

Opening an invite link shows the invitee a signup page with the welcome message and a PIN field when one is set.

![Invite signup page](screenshots/Invite%20Signup.png)

## Disabled Invites

A group whose [password changes](Passwords.md#password-changes) are set to **Disallowed** cannot be used for invites, and switching a group to that mode disables its outstanding invite links. A disabled invite, or one past its expiration, cannot be manually reenabled until the cause is fixed.

!!! tip
    To prevent abuse, it is recommended to create a new link for each user that you want to onboard!
