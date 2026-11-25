import { readFile, appendFile } from 'node:fs/promises';
import { setTimeout as delay } from 'node:timers/promises';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

export const REPOSITORY = 'remicks/LootDrop-Windows';
const API = `https://api.github.com/repos/${REPOSITORY}`;
const RELEASES = `https://github.com/${REPOSITORY}/releases`;
const TAG = /^v?[0-9]{1,6}\.[0-9]{1,6}\.[0-9]{1,6}(?:\.[0-9]{1,6})?$/;
const DIGEST = /^sha256:[a-f0-9]{64}$/i;
export class SafeError extends Error {}

export function requiredNames(tag) {
  if (typeof tag !== 'string' || !TAG.test(tag)) throw new SafeError('Unsupported release tag. Use a numeric LootDrop version tag.');
  const version = tag.replace(/^v/, '');
  return [`LootDrop-v2-${version}-Setup.exe`, `LootDrop-v2-${version}-Portable-win-x64.zip`,
    `LootDrop-v2-Windows-${version}-Project.zip`, `LootDrop-v2-${version}-Release-Report.md`,
    `LootDrop-v2-${version}-SHA256SUMS.txt`, `LootDrop-v2-${version}-Archive-Validation.txt`];
}
export function readiness(release) {
  if (!release || release.draft !== false || typeof release.prerelease !== 'boolean' || !Number.isSafeInteger(release.id) || release.id <= 0 || !Number.isFinite(Date.parse(release.published_at)))
    return { ready: false, reason: 'Release is not published.' };
  let names;
  try { names = requiredNames(release.tag_name); } catch { return { ready: false, reason: 'Unsupported version tag.' }; }
  if (release.html_url !== `${RELEASES}/tag/${release.tag_name}`) throw new SafeError('Release does not belong to the configured repository.');
  if (!Array.isArray(release.assets) || release.assets.length > 100) return { ready: false, reason: 'Invalid asset list.' };
  const unique = new Set(release.assets.map(a => a?.name));
  if (unique.size !== release.assets.length) return { ready: false, reason: 'Duplicate asset names.' };
  const missing = names.filter(name => !unique.has(name));
  if (missing.length) return { ready: false, reason: `Waiting for required assets: ${missing.join(', ')}` };
  const unfinished = release.assets.some(a => !a || typeof a.name !== 'string' || !Number.isSafeInteger(a.id) || a.id <= 0 ||
    a.state !== 'uploaded' || !Number.isSafeInteger(a.size) || a.size <= 0 || !DIGEST.test(a.digest ?? '') ||
    a.browser_download_url !== `https://github.com/${REPOSITORY}/releases/download/${release.tag_name}/${encodeURIComponent(a.name)}`);
  if (unfinished) return { ready: false, reason: 'Waiting for every attached asset to finish uploading and receive its digest.' };
  const fingerprint = JSON.stringify([release.id, release.updated_at,
    [...release.assets].sort((a, b) => a.name.localeCompare(b.name)).map(a => [a.id, a.name, a.size, a.digest, a.updated_at])]);
  return { ready: true, fingerprint };
}
export async function waitForReady(load, { now = Date.now, sleep = delay, log = console.log, timeoutMs = 600_000, quietMs = 30_000, pollMs = 10_000 } = {}) {
  const started = now(); let fingerprint; let stableSince; let observations = 0;
  while (now() - started < timeoutMs) {
    const release = await load(); const state = readiness(release);
    if (state.ready) {
      if (fingerprint !== state.fingerprint) { fingerprint = state.fingerprint; stableSince = now(); observations = 1; }
      else observations++;
      if (observations >= 2 && now() - stableSince >= quietMs) return release;
      log('All release assets uploaded; waiting for the 30-second quiet period.');
    } else { fingerprint = undefined; stableSince = undefined; observations = 0; log(state.reason); }
    await sleep(pollMs);
  }
  throw new SafeError('Uploads did not finish within ten minutes. No Discord message was sent. Finish uploading, then rerun manually.');
}
// Keep announcements compact without a network summarizer or another dependency.
// Authors can supply a dedicated "Discord summary" section; older notes use feature headings/bullets.
const SUMMARY_ITEMS = 3;
const SUMMARY_ITEM_LENGTH = 110;
function summaryText(value) {
  const text = value.replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1').replace(/[*_\x60~]/g, '')
    .replace(/\s+/g, ' ').trim();
  if (text.length <= SUMMARY_ITEM_LENGTH) return text;
  const shortened = text.slice(0, SUMMARY_ITEM_LENGTH - 1).replace(/[\uD800-\uDBFF]$/, '');
  const boundary = shortened.lastIndexOf(' ');
  return shortened.slice(0, boundary >= SUMMARY_ITEM_LENGTH / 2 ? boundary : undefined).trimEnd() + '…';
}
export function summarizeNotes(body) {
  const sections = [{ title: '', lines: [] }];
  let fence;
  for (const line of (typeof body === 'string' ? body : '').slice(0, 20_000).replace(/\r/g, '').split('\n')) {
    const delimiter = line.match(/^\s*(\x60{3,}|~{3,})/);
    if (delimiter) { if (!fence) fence = delimiter[1][0]; else if (fence === delimiter[1][0]) fence = undefined; continue; }
    if (fence) continue;
    const heading = line.match(/^\s{0,3}#{1,6}\s+(.+?)\s*#*\s*$/);
    if (heading) sections.push({ title: summaryText(heading[1]), lines: [] });
    else sections.at(-1).lines.push(line);
  }
  const dedicated = sections.find(s => /^(?:discord|announcement) summary$/i.test(s.title));
  const highlights = [];
  const append = text => {
    const clean = summaryText(text);
    if (clean && !highlights.includes(clean) && highlights.length < SUMMARY_ITEMS) highlights.push(clean);
  };
  for (const section of dedicated ? [dedicated] : sections) {
    if (!dedicated && /^(?:validation|testing|tests|safety|security|downloads?|installation|checksums?|known issues|remaining coverage|release status|reproducing validation)\b/i.test(section.title)) continue;
    const generic = !section.title || /^LootDrop\b/i.test(section.title) || /^(?:what.?s new|changelog|changes|highlights|features|improvements|fixes|bug fixes|release notes|overview|summary|added|updated|changed)$/i.test(section.title);
    if (!dedicated && !generic) append(section.title);
    else {
      const bullets = section.lines.filter(line => /^\s*(?:[-*+] |\d+[.)] )/.test(line));
      if (bullets.length) for (const line of bullets) append(line.replace(/^\s*(?:[-*+] |\d+[.)] )(?:\[[ xX]\]\s*)?/, ''));
      else for (const paragraph of section.lines.join('\n').split(/\n\s*\n/)) {
        const text = paragraph.replace(/\s+/g, ' ').trim();
        if (text && !/^!\[|^<\!--|^\|/.test(text)) append(text.match(/^.*?[.!?](?:\s|$)/)?.[0] || text);
      }
    }
    if (highlights.length === SUMMARY_ITEMS) break;
  }
  return highlights.length ? highlights.map(text => '• ' + text).join('\n') : 'See the release notes for changes.';
}

export function buildPayload(release) {
  if (!readiness(release).ready) throw new SafeError('Cannot announce an incomplete release.');
  const version = release.tag_name.replace(/^v/, '');
  const asset = suffix => release.assets.find(a => a.name === `LootDrop-v2-${version}-${suffix}`).browser_download_url;
  const kind = release.prerelease ? 'Preview release' : 'New release';
  const notes = summarizeNotes(release.body);
  const link = `\n\n[Full release notes and all downloads](${release.html_url})`;
  return {
    username: 'LootDrop Releases',
    avatar_url: `https://raw.githubusercontent.com/${REPOSITORY}/main/Assets/Branding/degenerates-inc-duo.png`,
    allowed_mentions: { parse: [], users: [], roles: [], replied_user: false },
    embeds: [{
      title: String(release.name || `LootDrop ${release.tag_name}`).slice(0, 256),
      // Match the RMPCS announcement accent and single-column link-first layout.
      url: release.html_url, color: 0xBE2B4E,
      description: `**${kind}**\n\n[Install LootDrop](${asset('Setup.exe')}) | [Portable ZIP](${asset('Portable-win-x64.zip')})\n` +
        `[Release report](${asset('Release-Report.md')}) | [Checksums](${asset('SHA256SUMS.txt')})\n\n${notes}` + link,
      footer: { text: 'LootDrop · Your Free Game Radar' },
      timestamp: new Date(release.published_at).toISOString()
    }]
  };
}
export function webhookEndpoint(secret) {
  let url;
  try { url = new URL(secret); } catch { throw new SafeError('DISCORD_WEBHOOK_URL is missing or invalid.'); }
  if (url.protocol !== 'https:' || !['discord.com', 'discordapp.com'].includes(url.hostname) || url.port || url.username || url.password || url.hash ||
      !/^\/api(?:\/v[0-9]+)?\/webhooks\/[0-9]+\/[A-Za-z0-9._-]+\/?$/.test(url.pathname) || url.href.length > 1024)
    throw new SafeError('The webhook secret must be an official Discord HTTPS webhook URL.');
  for (const key of url.searchParams.keys()) if (!['wait', 'thread_id'].includes(key)) throw new SafeError('Unsupported webhook query option.');
  if (url.searchParams.has('thread_id') && !/^[0-9]+$/.test(url.searchParams.get('thread_id'))) throw new SafeError('Invalid Discord thread identifier.');
  url.searchParams.set('wait', 'true'); return url;
}
async function boundedJson(response, label) {
  if (!response.body) throw new SafeError(`${label} returned an empty response.`);
  const reader = response.body.getReader(); const chunks = []; let size = 0;
  try {
    for (;;) { const { value, done } = await reader.read(); if (done) break; size += value.length;
      if (size > 2 * 1024 * 1024) throw new SafeError(`${label} response exceeded the limit.`); chunks.push(value); }
    try { return JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { throw new SafeError(`${label} returned invalid JSON.`); }
  } finally { await reader.cancel().catch(() => {}); }
}
export async function sendDiscord(secret, payload, { fetcher = fetch, sleep = delay } = {}) {
  const endpoint = webhookEndpoint(secret);
  for (let attempt = 0; attempt < 4; attempt++) {
    let response;
    try { response = await fetcher(endpoint, { method: 'POST', redirect: 'error', signal: AbortSignal.timeout(30_000),
      headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) }); }
    catch { throw new SafeError('Discord request failed. Its delivery is uncertain; inspect the channel before retrying. Webhook details were not logged.'); }
    if (response.status === 429 && attempt < 3) {
      const seconds = Number(response.headers.get('retry-after') || '1');
      await response.body?.cancel(); await sleep(Math.min(30, Math.max(1, Number.isFinite(seconds) ? seconds : 1)) * 1000); continue;
    }
    if (!response.ok) { await response.body?.cancel(); throw new SafeError(`Discord rejected the announcement (HTTP ${response.status}). Check the secret and channel permissions.`); }
    const message = await boundedJson(response, 'Discord');
    if (!message.id || message.mention_everyone !== false || message.mentions?.length || message.mention_roles?.length)
      throw new SafeError('Discord returned unexpected delivery/mention details; inspect the channel before retrying.');
    return;
  }
}
export async function main(env = process.env) {
  if (env.GITHUB_REPOSITORY !== REPOSITORY) throw new SafeError('Notification workflow is restricted to LootDrop-Windows.');
  const event = JSON.parse(await readFile(env.GITHUB_EVENT_PATH, 'utf8'));
  let endpoint;
  if (env.GITHUB_EVENT_NAME === 'release' && event.action === 'published' && Number.isSafeInteger(event.release?.id) && event.release.id > 0)
    endpoint = `${API}/releases/${event.release.id}`;
  else if (env.GITHUB_EVENT_NAME === 'workflow_dispatch') { requiredNames(env.RELEASE_TAG); endpoint = `${API}/releases/tags/${encodeURIComponent(env.RELEASE_TAG)}`; }
  else throw new SafeError('Unsupported workflow event.');
  if (!env.GITHUB_TOKEN) throw new SafeError('GitHub read token is unavailable.');
  let expectedId;
  const load = async () => {
    let response;
    try { response = await fetch(endpoint, { redirect: 'error', signal: AbortSignal.timeout(20_000), headers: {
      Accept: 'application/vnd.github+json', Authorization: `Bearer ${env.GITHUB_TOKEN}`, 'User-Agent': 'LootDrop-release-announcement' } }); }
    catch { throw new SafeError('GitHub metadata request failed. No announcement was sent.'); }
    if (!response.ok) { await response.body?.cancel(); throw new SafeError(`GitHub metadata unavailable (HTTP ${response.status}); no announcement was sent.`); }
    const release = await boundedJson(response, 'GitHub');
    expectedId ??= release.id;
    if (release.id !== expectedId) throw new SafeError('Release identity changed while waiting. No announcement was sent.');
    return release;
  };
  const release = await waitForReady(load); const payload = buildPayload(release);
  if (env.DRY_RUN === 'true') {
    console.log(`Dry run passed for ${release.tag_name}: complete assets, no webhook call, all mentions blocked.`);
    if (env.GITHUB_STEP_SUMMARY) await appendFile(env.GITHUB_STEP_SUMMARY, `## Discord dry run passed\n\n[${release.tag_name}](${release.html_url}) has finished uploads. No Discord message was sent. All mention categories are disabled.\n`);
    return;
  }
  await sendDiscord(env.DISCORD_WEBHOOK_URL, payload);
  console.log(`Discord accepted the ${release.tag_name} release announcement with no mentions. Webhook and channel details were not logged.`);
  if (env.GITHUB_STEP_SUMMARY) await appendFile(env.GITHUB_STEP_SUMMARY, `## Discord release announcement delivered\n\n[${release.tag_name}](${release.html_url}) was announced after upload completion. Everyone, role and user mentions were disabled.\n`);
}
if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  main().catch(error => { console.error(error instanceof SafeError ? error.message : 'Unexpected notification error. No credential details were logged.'); process.exitCode = 1; });
}
