# Invites

Create a shareable signup link tied to a group. Anyone with the link can create their own account on your server. All users that use the link are created using the group assigned to it. For added security, an optional PIN can be set, and the user will have to provide it to use the link. Additionally, you can set a rate limit on how many times the link can be used over a period of time to avoid spam.

New invites are made from the **Create Invite** section of the Invites tab.

![Create Invite form](screenshots/Create%20Invite.png)

## Group

Pick either a specific group or **Default group**. A specific group is fixed for the life of the link. **Default group** resolves at the moment the link is redeemed, so the invite follows whichever group is marked default on the Groups tab, even if that changes after the link is sent.

An account created through an invite gets exactly the invite's group: its permissions, preferences and [password rules](Passwords.md). The default group is never applied to it on the way, even when the invite points at a different group. If an invite's specific group is deleted later, new accounts fall back to the default group.

A group that has reached its [expiration date](Expiration.md) cannot take new accounts. Redeeming an invite into an expired group is refused with a message saying so, and the Invites tab marks such invites **Group expired** and warns when you pick an expired group for a new invite. Move or clear the group's expiration to bring its invites back.

## Options

Each invite can also carry:

* **Name**: An admin name for the invite. It is only shown on the dashboard, so this field is for admin reference only.
* **Welcome message**: Shown to the invitee under the signup heading.
* **Resources**: Title and URL pairs presented as link buttons after the account is created, for example a request site or a getting started guide. Only absolute http(s) URLs are accepted.

## Managing Invites

Every invite is listed with its link, usage, group and expiration, along with buttons to copy, lock or delete it.

![Invite list](screenshots/Invite%20List.png)

**Manage Access** sets the public server URL used to build links and the signup rate limit applied to each invite.

![Manage Access settings](screenshots/Manage%20Access.png)

## Signup Page

Opening an invite link shows the invitee a signup page with the welcome message and a PIN field when one is set.

![Invite signup page](screenshots/Invite%20Signup.png)

## Limits

!!! warning "Reverse proxy users: add your proxy to Known proxies"
    The per address limit below counts each visitor by the address Jellyfin sees. Behind a reverse proxy, Jellyfin only sees the real visitor when the proxy is listed under **Dashboard > Networking > Known proxies**. Otherwise every visitor appears to come from the proxy and shares one budget, so a single person, or anyone trying to cause trouble, can block every invite for everyone for 15 minutes with a handful of failed attempts. The server logs a warning once when it sees invite requests that look like they come through an untrusted proxy.

Invite links are public, so a few limits apply to them:

* **PIN lockout**: After 5 wrong PINs an invite locks itself, whoever entered them. Anyone holding the link can enter PINs, so someone you did not send it to can lock it on purpose. A locked invite stays locked until you enable it again on the Invites tab, which also resets the count.
* **Signup rate limit**: The **Invite rate limit** under **Manage Access** pauses an invite once it has created that many accounts within the window, then lets it work again. It counts successful signups per invite, from every address. It defaults to 10 signups per 60 minutes. Servers whose saved plugin settings already include a rate limit keep it (off, unless you changed it). A server upgraded from a version before the rate limit existed that has not saved its plugin settings since picks up the default. Set either number to 0 to turn it off.
* **Per address limit**: Each network address may open at most 60 invite pages and make at most 10 signup attempts every 15 minutes, across all invites. Every attempt counts, including a wrong PIN, a taken username or a rejected password. Past that, the server answers with "Too many attempts" until the oldest attempt is 15 minutes old. IPv6 visitors are counted per /64 block. These numbers are fixed and reset when the server restarts.

## Disabled Invites

A group whose [password changes](Passwords.md#password-changes) are set to **Disallowed** cannot be used for invites, and switching a group to that mode disables its outstanding invite links. A disabled invite, or one past its expiration, cannot be manually reenabled until the cause is fixed.

!!! tip
    To prevent abuse, it is recommended to create a new link for each user that you want to onboard!
