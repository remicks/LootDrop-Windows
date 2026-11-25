import test from 'node:test';
import assert from 'node:assert/strict';
import { buildPayload, summarizeNotes, readiness, requiredNames, waitForReady, webhookEndpoint, sendDiscord, SafeError } from './announce-release.mjs';
const WEBHOOK = 'https://discord.com/api/webhooks/123456789/test-placeholder-token';
const fixture = () => ({ id: 42, tag_name: 'v0.7.11', name: 'LootDrop v0.7.11', draft: false, prerelease: true,
  published_at: '2026-11-22T04:29:00Z', updated_at: '2026-11-22T04:29:00Z', body: 'Notes @everyone @here <@&123> <@456>',
  html_url: 'https://github.com/remicks/LootDrop-Windows/releases/tag/v0.7.11',
  assets: requiredNames('v0.7.11').map((name, index) => ({ id: index + 1, name, size: 100, state: 'uploaded',
    digest: 'sha256:' + 'a'.repeat(64), updated_at: '2026-11-22T04:28:00Z',
    browser_download_url: 'https://github.com/remicks/LootDrop-Windows/releases/download/v0.7.11/' + name })) });

test('published prereleases and public releases are both eligible', () => {
  const r = fixture(); assert.equal(readiness(r).ready, true); r.prerelease = false; assert.equal(readiness(r).ready, true);
});
test('drafts, missing packages, zero-size uploads and absent digests are not announced', () => {
  let r = fixture(); r.draft = true; assert.equal(readiness(r).ready, false);
  r = fixture(); r.assets.pop(); assert.equal(readiness(r).ready, false);
  r = fixture(); r.assets[0].size = 0; assert.equal(readiness(r).ready, false);
  r = fixture(); r.assets[0].digest = ''; assert.equal(readiness(r).ready, false);
  r = fixture(); r.assets[0].state = 'new'; assert.equal(readiness(r).ready, false);
});
test('unfinished optional assets also prevent announcing', () => {
  const r = fixture(); r.assets.push({ ...r.assets[0], id: 100, name: 'preview.png', state: 'new' }); assert.equal(readiness(r).ready, false);
});
test('duplicate asset names and foreign release/download URLs are rejected', () => {
  let r = fixture(); r.assets.push(r.assets[0]); assert.equal(readiness(r).ready, false);
  r = fixture(); r.assets[0].browser_download_url = 'https://evil.com/setup.exe'; assert.equal(readiness(r).ready, false);
  r = fixture(); r.html_url = 'https://github.com/other/repo/releases/tag/v0.7.11'; assert.throws(() => readiness(r), SafeError);
});
test('unsafe tags cannot inject paths or unexpected package names', () => {
  for (const tag of ['../other', 'v0.7.11\n', 'v1.2', 'v1.2.3;curl bad', null]) assert.throws(() => requiredNames(tag), SafeError);
});
test('waits for a full quiet period, not just an uploaded first response', async () => {
  let time = 0; let calls = 0;
  const r = await waitForReady(async () => { calls++; return fixture(); }, { now: () => time, sleep: async ms => { time += ms; }, log: () => {} });
  assert.equal(r.id, 42); assert.equal(time, 30_000); assert.equal(calls, 4);
});
test('a new optional asset resets the quiet period', async () => {
  let time = 0; let calls = 0;
  await waitForReady(async () => { calls++; const r = fixture(); if (calls >= 3) r.assets.push({ ...r.assets[0], id: 100, name: 'preview.png', browser_download_url: 'https://github.com/remicks/LootDrop-Windows/releases/download/v0.7.11/preview.png' }); return r; },
    { now: () => time, sleep: async ms => { time += ms; }, log: () => {} });
  assert.equal(time, 50_000);
});
test('missing uploads time out without an announcement', async () => {
  let time = 0; await assert.rejects(waitForReady(async () => ({ ...fixture(), assets: [] }),
    { now: () => time, sleep: async ms => { time += ms; }, log: () => {}, timeoutMs: 50 }), /No Discord message was sent/);
});
test('payload explicitly disables everyone, user and role mentions and has no content', () => {
  const payload = buildPayload(fixture()); assert.deepEqual(payload.allowed_mentions, { parse: [], users: [], roles: [], replied_user: false });
  assert.equal('content' in payload, false); assert.match(payload.embeds[0].description, /@everyone/);
  assert.match(payload.embeds[0].description, /Preview release/); assert.match(payload.embeds[0].description, /LootDrop-v2-0.7.11-Setup.exe/);
});
test('oversized public release notes/title are constrained to Discord limits', () => {
  const r = fixture(); r.name = 'x'.repeat(1000); r.body = 'y'.repeat(100000); r.prerelease = false;
  const p = buildPayload(r); assert.equal(p.embeds[0].title.length, 256); assert.ok(p.embeds[0].description.length <= 1600); assert.match(p.embeds[0].description, /Full release notes/);
});
test('incomplete release cannot bypass readiness at payload construction', () => assert.throws(() => buildPayload({ ...fixture(), draft: true }), SafeError));
test('webhook accepts only official HTTPS endpoints and safe thread options', () => {
  assert.equal(webhookEndpoint(WEBHOOK).searchParams.get('wait'), 'true');
  assert.equal(webhookEndpoint(WEBHOOK + '?thread_id=123').searchParams.get('thread_id'), '123');
  for (const url of ['http://discord.com/api/webhooks/1/token', 'https://discord.com.evil.com/api/webhooks/1/token', 'https://user@discord.com/api/webhooks/1/token', 'https://discord.com:444/api/webhooks/1/token', WEBHOOK + '?redirect=evil', WEBHOOK + '#fragment', 'not-a-url'])
    assert.throws(() => webhookEndpoint(url), SafeError);
});
test('Discord POST blocks redirects, omits GitHub authorization, and receives mention suppression', async () => {
  let count = 0;
  await sendDiscord(WEBHOOK, buildPayload(fixture()), { fetcher: async (url, options) => {
    count++; assert.equal(url.hostname, 'discord.com'); assert.equal(options.redirect, 'error'); assert.equal(options.headers.Authorization, undefined);
    assert.deepEqual(JSON.parse(options.body).allowed_mentions.parse, []);
    return Response.json({ id: '123', mention_everyone: false, mentions: [], mention_roles: [] });
  } }); assert.equal(count, 1);
});
test('ambiguous network failure is not retried and cannot reveal the webhook', async () => {
  let count = 0;
  await assert.rejects(sendDiscord(WEBHOOK, buildPayload(fixture()), { fetcher: async () => { count++; throw new Error(WEBHOOK); } }), error => {
    assert.ok(!error.message.includes('test-placeholder-token')); return true;
  }); assert.equal(count, 1);
});
test('HTTP errors cannot print Discord response contents or webhook token', async () => {
  await assert.rejects(sendDiscord(WEBHOOK, buildPayload(fixture()), { fetcher: async () => new Response(WEBHOOK, { status: 401 }) }), error => {
    assert.match(error.message, /401/); assert.ok(!error.message.includes('test-placeholder-token')); return true;
  });
});
test('only rejected rate-limited requests retry, with a bounded delay', async () => {
  let count = 0; const pauses = [];
  await sendDiscord(WEBHOOK, buildPayload(fixture()), { sleep: async ms => pauses.push(ms), fetcher: async () => {
    if (count++ === 0) return new Response('{}', { status: 429, headers: { 'retry-after': '999999' } });
    return Response.json({ id: '123', mention_everyone: false, mentions: [], mention_roles: [] });
  } }); assert.equal(count, 2); assert.deepEqual(pauses, [30_000]);
});
test('unexpected mention details are treated as a delivery verification error', async () => {
  await assert.rejects(sendDiscord(WEBHOOK, buildPayload(fixture()), { fetcher: async () => Response.json({ id: '123', mention_everyone: true }) }), /unexpected delivery\/mention/);
});


test('feature headings summarize a release without its validation and download prose', () => {
  const notes = '## Six new themes\n\nMidnight Slate and more palettes.\n\n## Five new embedded fonts\n\nFive font families and full licenses.\n\n## Validation and safety\n\n723 assertions and a very long report.\n\n## Downloads\n\nAll artifacts are attached.';
  assert.equal(summarizeNotes(notes), '• Six new themes\n• Five new embedded fonts');
  const description = buildPayload({ ...fixture(), body: notes }).embeds[0].description;
  assert.ok(description.length < 1100); assert.ok(!description.includes('723 assertions'));
  assert.match(description, /Full release notes and all downloads/);
});

test('a grouped changelog shows at most three short feature bullets', () => {
  assert.equal(summarizeNotes('## What’s new\n- Aligned cards.\n- Embedded fonts.\n- Categorized Settings.\n- Rounded switches.'),
    '• Aligned cards.\n• Embedded fonts.\n• Categorized Settings.');
});

test('a dedicated Discord summary takes priority over the full changelog', () => {
  const notes = '## Features\n- Full detail one\n- Full detail two\n- Full detail three\n\n## Discord summary\n- Cleaner layouts\n- More themes\n\n## Validation\nMany checks.';
  assert.equal(summarizeNotes(notes), '• Cleaner layouts\n• More themes');
  assert.equal(summarizeNotes('## Announcement summary\nShort curated update. More detail is elsewhere.'), '• Short curated update.');
});

test('markdown is flattened and fenced commands do not become highlights', () => {
  const notes = '## Highlights\n~~~sh\n- private build command\n~~~\n- **Faster** [search](https://example.org).\n- [x] Cleaner _cards_.';
  assert.equal(summarizeNotes(notes), '• Faster search.\n• Cleaner cards.');
});

test('plain paragraphs use short first sentences and empty notes have a fallback', () => {
  assert.equal(summarizeNotes('More font choices. Extra explanation stays on GitHub.\n\nClearer Settings. Long explanation.'), '• More font choices.\n• Clearer Settings.');
  for (const notes of [null, undefined, '', '## Validation\nOnly a report.', '## Discord summary\n'])
    assert.equal(summarizeNotes(notes), 'See the release notes for changes.');
});

test('long highlights are word-trimmed and the complete embed stays compact', () => {
  const notes = '## Highlights\n' + ['First','Second','Third','Fourth'].map(word => '- ' + word + ' useful words'.repeat(100)).join('\n');
  const summary = summarizeNotes(notes); assert.equal(summary.split('\n').length, 3);
  for (const line of summary.split('\n')) { assert.ok(line.length <= 112); assert.ok(line.endsWith('…')); assert.ok(!line.endsWith('usefu…')); }
  const payload = buildPayload({ ...fixture(), body: notes });
  assert.ok(payload.embeds[0].description.length <= 1600);
  assert.equal('fields' in payload.embeds[0], false);
  assert.deepEqual(payload.allowed_mentions, { parse: [], users: [], roles: [], replied_user: false });
});

test('summary work is bounded even when a release body is enormous', () => {
  assert.equal(summarizeNotes('## Highlights\n- Quick update\n~~~\n' + 'detail\n'.repeat(100000)), '• Quick update');
});


test('trimming unbroken emoji does not split a Unicode character', () => {
  const summary = summarizeNotes('## Highlights\n- ' + '😀'.repeat(100));
  assert.ok(summary.length <= 112); assert.ok(summary.endsWith('…'));
  assert.ok([...summary].every(character => !/[\uD800-\uDFFF]/.test(character) || character.length === 2));
});

test('a repeated release title does not crowd out the feature summary', () => {
  assert.equal(summarizeNotes('# LootDrop Windows v2 0.7.14\n\n## New themes\nMore detailed text.\n\n## New fonts\nMore detailed text.'), '• New themes\n• New fonts');
});


test('RMPCS layout uses one link-first embed with matching accent, short highlights and branded footer', () => {
  const release = { ...fixture(), body: '## Highlights\n- Cleaner layouts\n- More fonts\n- More themes\n- Fourth detail' };
  const payload = buildPayload(release);
  assert.equal(payload.username, 'LootDrop Releases');
  assert.equal(payload.embeds.length, 1);
  const embed = payload.embeds[0];
  assert.equal(embed.color, 12462926);
  assert.equal(embed.url, release.html_url);
  assert.equal(embed.footer.text, 'LootDrop · Your Free Game Radar');
  assert.equal(embed.timestamp, new Date(release.published_at).toISOString());
  assert.equal('fields' in embed, false);
  const download = 'https://github.com/remicks/LootDrop-Windows/releases/download/v0.7.11/LootDrop-v2-0.7.11-';
  assert.equal(embed.description,
    '**Preview release**\n\n' +
    '[Install LootDrop](' + download + 'Setup.exe) | [Portable ZIP](' + download + 'Portable-win-x64.zip)\n' +
    '[Release report](' + download + 'Release-Report.md) | [Checksums](' + download + 'SHA256SUMS.txt)\n\n' +
    '• Cleaner layouts\n• More fonts\n• More themes\n\n' +
    '[Full release notes and all downloads](' + release.html_url + ')');
  assert.match(buildPayload({ ...release, prerelease: false }).embeds[0].description, /^\*\*New release\*\*/);
});
