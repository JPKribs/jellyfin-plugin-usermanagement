# Glossary

The agreed name for every concept in this plugin. The UI, the docs, messages, and code use these words and no others.

* Use the **Term** in the UI, the docs, and messages.
* The **In Code** column names the type or member that holds it, when the code name differs.
* Never use a word from the **Not** column for the concept.
* Add a term here before it appears anywhere else. Rename it here first when it changes, then everywhere in the same commit.

## Groups

| Term | Meaning | In Code | Not |
| :--- | :--- | :--- | :--- |
| Group | A set of settings applied to every user in it | `GroupDefinition` | Role, template, profile |
| Member | A user in a group. A user belongs to one group at a time | `MemberIds` | Assignee, participant |
| Membership | The **Groups** section that picks a group's members | | Users list |
| Default group | The group new users join, marked **(default)** in the group select | `DefaultGroupId` | Primary group, fallback group |
| Group (pill) | The pill that makes a setting reach members. The group's value is written onto every member | `Manage<Setting>` is `true` | Override, manage, enforce, lock |
| Member (pill) | The pill that leaves a setting alone, so each member keeps their own value | `Manage<Setting>` is `false` | Inherit, unmanaged |
| Set by group | The count of a section's settings on **Group**, shown in its header as **2 set by group** | | Overrides, managed |
| Access and features | The **Groups** section of Jellyfin user policy settings, such as library access and parental control | `GroupPermissions` | Settings, permissions page, policy |
| Preferences | The **Groups** section of display and playback preferences, such as subtitles and home screen | `GroupConfiguration` | Configuration, preferences page |
| Account rules | The **Groups** section of session management, password requirements, expiry, and inactivity | | Other, lifecycle, extras |
| Access schedule | The day and time windows when members may use the server | `AccessSchedules`, `AccessScheduleEntry` | Time limit, curfew |
| Media library order | The order libraries appear on the home screen | `OrderedViews` | Library sort |
| Apply | Writes every group's overridden settings onto its members, on save and on a schedule | `GroupService`, `POST /UserManagement/Apply` | Sync, push |

## Passwords

| Term | Meaning | In Code | Not |
| :--- | :--- | :--- | :--- |
| Password requirements | A group's password rules, such as minimum length and required characters | `PasswordPolicy` | Password policy, complexity rules |
| Password changes | Whether members may set or change their own password | `PasswordChangeMode` | Password mode |
| Allowed | Members manage their own password, within the rules | `PasswordChangeMode.Allowed` | Enabled |
| Initial password only | A member with an empty password may set one, and nobody may change an existing one | `PasswordChangeMode.InitialOnly` | First password only |
| Disallowed | Only administrators set passwords. Such a group cannot take invites | `PasswordChangeMode.Disallowed` | Locked, admin only |
| Disallow empty passwords | Refuses an empty password, which otherwise passes every rule | `DisallowEmpty` | Require password |
| Authentication provider | The provider that checks member passwords against the rules | `PasswordRuleAuthenticationProvider` | Auth plugin, validator |
| Enrollment | Moving a member onto the authentication provider, keeping their original provider to return to | `ProviderEnrollment` | Registration |
| Exempt | Administrators, whom group rules, expiry, inactivity, and session cleanup never touch | `AdminExemption` | Excluded, immune |

## Lifecycle

| Term | Meaning | In Code | Not |
| :--- | :--- | :--- | :--- |
| Expiry | The date a group's members are disabled or deleted | `ExpiresOn` | Expiration date, end date |
| Expiration method | What expiry does to members, **Disable** or **Delete** | `GroupExpiryAction` | Expiry action, mode |
| Inactivity | Disabling members who have not been active for a number of days | `DisableInactiveUsers`, `InactiveDays` | Idle, dormancy |
| Session cleanup | Logging out a member's devices that have not checked in within a rule's window | `CleanupSessions` | Session pruning, logout |
| Rule | One session cleanup window, with its clients and days | `SessionCleanupRule` | Policy, filter |
| Clients | Which apps a rule covers, as **All clients**, **Only these clients**, or **All except these clients** | `SessionCleanupClientMode` | Apps, devices |

## Invites

| Term | Meaning | In Code | Not |
| :--- | :--- | :--- | :--- |
| Invite | A shareable signup link that creates one account per use in its group | `Invite` | Invitation, signup code |
| Invite link | The public address of an invite, built from the Server URL and the token | `Token` | Signup URL, invite URL |
| Invite card | One invite on the **Invites** tab, with its name, status, details, and link | `renderCard` | Invite row, tile |
| QR code | A scannable image of an invite link, opened by the QR button beside the link | `createQrCode` | Barcode, QR image |
| Name | An invite's label, shown only on the dashboard | `Label` | Title, description |
| Welcome message | The text shown under the signup heading | `Message` | Greeting, note |
| Resources | Title and URL pairs shown as link buttons after signup | `InviteResource` | Links, bookmarks |
| PIN | A six digit code the invitee must enter | `PinHash` | Password, passcode |
| PIN lockout | Locking an invite after too many wrong PINs | `MaxPinAttempts` | PIN ban |
| Server URL | The public address invite links are built from | `InviteBaseUrl` | Base URL, public URL |
| Invite rate limit | Signups per invite allowed within a window, after which the link pauses | `InviteRateLimitCount`, `InviteRateLimitWindowMinutes` | Throttle |
| Per address limit | The fixed cap on invite requests from one network address | `InviteAttemptLimiter` | IP ban, flood guard |
| Signup page | The public page an invite link opens | `usermanagement_invite_public.html` | Landing page, redeem page |
| Redeem | Creating an account through an invite | `Redeem` | Use, claim, consume |
| Invite status | The per invite counters of uses, wrong PINs, and recent signups, kept in their own file | `InviteStatusStore` | Invite state, stats |

### Invite Statuses

| Term | Meaning | In Code |
| :--- | :--- | :--- |
| Active | The link works | `Enabled` |
| Locked | The invite is disabled, by hand or by PIN lockout | `Enabled` is `false` |
| Used up | The invite reached its maximum uses. The count card calls these **Consumed** | `MaxUses` |
| Expired | The invite reached its expiration date | `ExpiresAt` |
| Group expired | The invite's group has reached its expiry, so signups are refused | |

## Password Resets

| Term | Meaning | In Code | Not |
| :--- | :--- | :--- | :--- |
| Reset code | The code Jellyfin's **Forgot Password** flow writes to a file on the server | `ResetCodeInfo` | Reset PIN, token |
| Reset code extraction | The **Resets** tab setting that reads and lists reset codes | `EnableResetCodeExtraction` | Reset viewer |
| Pending reset | A reset code that has not been used | | Open reset |

## Housekeeping

| Term | Meaning | Not |
| :--- | :--- | :--- |
| Activity log | Jellyfin's dashboard activity log, where the plugin writes its events | Audit log, event feed |
| Server log | Jellyfin's log files | Debug log |
| Add users to groups | The scheduled task that puts users without a group into the default group | Assignment task |
| Apply group permissions | The scheduled task that applies every group to its members | Sync task |
| Process expired and inactive users | The scheduled task for expiry, inactivity, and expired invites | Expiry task |
| Clean expired sessions | The scheduled task that runs session cleanup | Session task |
