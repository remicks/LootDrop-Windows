# Discord release announcements

The GitHub workflow `.github/workflows/discord-release.yml` announces completed LootDrop Windows releases to the channel selected by the repository's `DISCORD_WEBHOOK_URL` Actions secret. It includes both public releases and tester prereleases. Nothing is added to the desktop application, and the webhook is never included in the EXE, source code, release assets or logs.

## Publication policy

Every build published as a GitHub Release must be announced in Discord, including every unsigned/tester prerelease. Do not treat a local package as a completed published delivery. Upload all artifacts to a draft, publish it last, then verify the announcement job succeeded. Everyone, role and user pings remain disabled. Drafts are not published builds and must not announce unfinished uploads. If a future publisher uses GITHUB_TOKEN, it must explicitly dispatch the notifier after publication because that token may suppress the release-triggered workflow. Check for an existing successful announcement before retrying to avoid duplicate posts.

## Normal publishing

1. Create a **draft** release with a supported numeric tag such as `v0.7.12`.
2. Upload all artifacts, then publish the release **last**. GitHub has no general "all future uploads finished" event; publishing the completed draft is the completion signal.
3. The notification workflow refreshes the release from GitHub. It requires the versioned installer, portable ZIP, full project ZIP, release report, SHA256SUMS and archive-validation note. Every attached asset (including optional previews) must have uploaded state, nonzero size, the correct repository download URL and a SHA256 digest.
4. After the asset list and metadata remain unchanged for 30 seconds, the workflow posts the announcement. New/changed/incomplete assets reset the waiting period. Missing uploads time out after ten minutes without posting.

Stable and prerelease events are both covered by `release: published`. Editing release notes or adding files later does not automatically reannounce a release, so finish uploading before publication. If a future automated publisher uses GitHub's built-in GITHUB_TOKEN, its release event might not trigger another workflow; explicitly dispatch this notification workflow after upload completion in that publishing process.

The message contains a branded release title, a short changelog summary, installer/portable links and a full-release link. The summary contains at most three highlights, each capped at 110 characters; the full changelog stays on GitHub. An optional `## Discord summary` (or `## Announcement summary`) section can provide curated bullet points. Without that section, feature headings or the first feature bullets are used, while validation/download sections and fenced code blocks are skipped. Long highlights are shortened at word boundaries. Empty notes get a short fallback. No external summarization service or new dependency is used. There is no top-level message content. Discord `allowed_mentions` explicitly disables everyone, role and user mentions, including when release notes contain mention syntax.

## Manual validation or recovery

Open Actions > Announce completed releases to Discord > Run workflow. Enter the release tag. Leave **dry_run** enabled to validate readiness without contacting the webhook. Turn dry_run off only when you want an announcement for that published release. Manual runs or reruns can intentionally announce the same release again; check the channel before repeating a run after uncertain network delivery.

## Implementation and permissions

The requested Marketplace action was reviewed at commit `24d166886aee4646d448c8a389ff9e1ebcab3682` (v1.20.0). Its action metadata specifies Node 16 and it does not expose an `allowed_mentions` control. This repository uses a small Node standard-library script instead, with no npm installation or third-party notification package. The only referenced action is official checkout, pinned to immutable commit `d23441a48e516b6c34aea4fa41551a30e30af803` (v6), with credential persistence disabled and sparse checkout limited to the notification code.

Workflow permissions are contents:read. No repository write permission, personal access token, scheduled poll or app account connection is needed. The webhook secret is passed only to the notification step. URLs are limited to the fixed GitHub repository and official Discord HTTPS webhook endpoints; redirects are rejected. Responses have time/size limits. Webhook URLs, tokens, response contents and Discord channel identifiers are not printed. Only rejected HTTP 429 requests are retried, with bounded attempts/delay; ambiguous delivery failures do not retry automatically. Discord's response must confirm a message without everyone/user/role mentions before success is logged.

The workflow itself cannot predict assets that a publisher decides to add after the quiet period. The draft-upload-publish-last process is essential. The fixed required filenames follow the existing Windows release packaging contract; adjust the readiness gate deliberately if that contract changes.

## Local tests

```text
node --test .github/scripts/announce-release.test.mjs
```

26 offline tests cover public/prerelease readiness, missing/unfinished/duplicate/foreign assets, version-tag injection, quiet-period timing/reset, timeout, explicit mention suppression, embed length limits, webhook-domain restrictions, redirect/auth-header handling, secret-safe failures, bounded rate-limit retries and Discord delivery checks. Summary checks additionally cover curated sections, feature extraction, Markdown/code handling, empty notes, size limits, bounded input and Unicode-safe shortening. No real Discord call is made by these tests.
