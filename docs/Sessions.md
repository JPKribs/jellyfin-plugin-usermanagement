# Sessions

Enable **Clean up old sessions** on a group and its members' stale devices are logged out by the `Clean Expired Sessions` **Scheduled Task**. A device is stale when it has not checked back into the server within a rule's window, so devices in active use are never touched.

## Rules

Cleanup is driven by one or more **rules**. Each rule sets a number of days and which clients it covers:

* **All Clients**: Every client is cleaned up by this rule.
* **Only These Clients**: Only sessions on the selected clients are removed during cleanup.
* **All Except These Clients**: All sessions are cleaned up except sessions on the selected clients.

Multiple rules let different clients age out on different schedules, for example cleaning up Jellyfin Web after a week while giving TV apps three months. When several rules cover the same client, **the shortest window wins**, and the dashboard points out which clients overlap. The client list is built from the devices that have connected to your server, plus any clients already selected in a rule, so a saved selection never disappears when its devices do.

Administrators are exempt: their devices are never logged out by session cleanup.
