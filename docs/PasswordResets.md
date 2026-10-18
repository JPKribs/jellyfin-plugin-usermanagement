# Password Resets

When a user starts Jellyfin's **Forgot Password** flow, the server writes a reset code to a file on its filesystem. The **Resets** tab can surface those codes so an administrator can pass one along without needing file access to the server. Codes are masked on screen until revealed, and can be copied directly.

The feature is **off by default** and must be enabled on the tab.

!!! warning
    **Only enable it when the dashboard is reached over HTTPS**, since over plain http the codes are readable by anyone watching the connection.
