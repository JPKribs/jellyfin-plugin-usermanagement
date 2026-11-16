// Captures the User Management documentation screenshots at 2x into docs/screenshots.
// Run python3 -I server.py in the background, then node shoot.mjs [file name ...] to take all or only the named ones.
// PLAYWRIGHT_CORE points at playwright-core's index.mjs, CHROMIUM at the Chromium binary, PORT at the server (default 8842).
import { existsSync } from 'node:fs';
import { homedir } from 'node:os';
import { fileURLToPath } from 'node:url';

let chromium;
try {
  ({ chromium } = await import(process.env.PLAYWRIGHT_CORE || 'playwright-core'));
} catch (e) {
  console.error(`Cannot load playwright-core (${e.message}). Set PLAYWRIGHT_CORE to playwright-core's index.mjs.`);
  process.exit(1);
}
const exe = process.env.CHROMIUM || `${homedir()}/Library/Caches/ms-playwright/chromium-1148/chrome-mac/Chromium.app/Contents/MacOS/Chromium`;
if (!existsSync(exe)) {
  console.error(`No Chromium at ${exe}. Set CHROMIUM to a Chromium binary.`);
  process.exit(1);
}
const OUT = fileURLToPath(new URL('../../docs/screenshots/', import.meta.url));
const ROOT = `http://127.0.0.1:${process.env.PORT || 8842}/`;

const b = await chromium.launch({ executablePath: exe });

// The Groups tab at 1280 px: the page from the form to the members section, or one group setting section.
async function groups(s) {
  const p = await b.newPage({ viewport: { width: 1280, height: 900 }, deviceScaleFactor: 2 });
  await p.goto(ROOT + 'groups.html');
  await p.waitForFunction(() => document.querySelectorAll('#selectGroup option').length > 0);
  await p.waitForTimeout(600);
  await p.evaluate((g) => { const e = document.querySelector('#selectGroup'); const o = [...e.options].find(o => o.textContent.startsWith(g)); e.value = o.value; e.dispatchEvent(new Event('change', { bubbles: true })); }, s.group);
  await p.waitForTimeout(400);
  if (s.page) {
    const r = await p.evaluate(() => {
      const form = document.querySelector('#UserManagementGroupsForm').getBoundingClientRect();
      const sec = document.querySelector('#memberMount').closest('.base-section');
      return { top: form.top + scrollY, bottom: sec.getBoundingClientRect().bottom + scrollY, left: form.left, w: form.width };
    });
    await p.screenshot({ path: `${OUT}${s.file}.png`, fullPage: true, clip: { x: Math.max(0, r.left - 8), y: Math.max(0, r.top - 8), width: r.w + 16, height: r.bottom - r.top + 16 } });
  } else {
    const h = await p.evaluateHandle((t) => [...document.querySelectorAll('#groupEditor .base-collapsible-section')].find(x => x.querySelector('.base-collapsible-title').textContent.trim() === t), s.section);
    await p.evaluate((el) => { const hd = el.querySelector('.base-collapsible-header'); if (hd.classList.contains('collapsed')) hd.click(); }, h);
    await p.waitForTimeout(400);
    await h.asElement().screenshot({ path: `${OUT}${s.file}.png` });
  }
  return p;
}

// The Invites tab at the given window size, once its invites have drawn.
async function invites(w, h) {
  const p = await b.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 2 });
  await p.goto(ROOT + 'invites.html');
  await p.waitForFunction(() => document.querySelectorAll('.um-invite').length > 0);
  await p.waitForTimeout(800);
  return p;
}

// One collapsible section of the Invites tab at 1280 px, opened.
async function inviteSection(s) {
  const p = await invites(1280, 900);
  const h = await p.evaluateHandle((t) => [...document.querySelectorAll('.base-collapsible-section')].find(x => x.querySelector('.base-collapsible-title').textContent.trim() === t), s.section);
  await p.evaluate((el) => { const hd = el.querySelector('.base-collapsible-header'); if (hd.classList.contains('collapsed')) hd.click(); }, h);
  await p.waitForTimeout(400);
  const r = await p.evaluate((el) => { const b = el.getBoundingClientRect(); return { x: b.left, y: b.top + scrollY, w: b.width, h: b.height }; }, h);
  await p.screenshot({ path: `${OUT}${s.file}.png`, fullPage: true, clip: { x: r.x - 8, y: r.y - 8, width: r.w + 16, height: r.h + 16 } });
  return p;
}

const shots = [
  {
    file: 'Overview', run: async (s) => {
      const p = await b.newPage({ viewport: { width: 1280, height: 900 }, deviceScaleFactor: 2 });
      await p.goto(ROOT + 'groups.html');
      await p.waitForFunction(() => document.querySelectorAll('#selectGroup option').length > 0);
      await p.waitForTimeout(800);
      // From the title to the group's buttons, above Membership.
      const r = await p.evaluate(() => {
        const form = document.querySelector('#UserManagementGroupsForm').getBoundingClientRect();
        const members = document.querySelector('#memberMount').closest('.base-section').getBoundingClientRect();
        return { x: form.left, y: form.top + scrollY, w: form.width, h: members.top - form.top };
      });
      await p.screenshot({ path: `${OUT}${s.file}.png`, clip: { x: Math.max(0, r.x - 8), y: Math.max(0, r.y - 8), width: r.w + 16, height: r.h + 8 } });
      return p;
    }
  },
  { file: 'Groups', group: 'Kids', page: true, run: groups },
  { file: 'Group Settings', group: 'Kids', section: 'Media Playback', run: groups },
  { file: 'Expiry', group: 'Guests', section: 'Expiry', run: groups },
  { file: 'Inactivity', group: 'Kids', section: 'Inactivity', run: groups },
  { file: 'Sessions', group: 'Kids', section: 'Session Management', run: groups },
  { file: 'Password Requirements', group: 'Kids', section: 'Password Requirements', run: groups },
  {
    file: 'Invite List', run: async (s) => {
      const p = await invites(1280, 900);
      const sec = await p.evaluateHandle(() => document.querySelector('#inviteList').closest('.base-collapsible-section'));
      await sec.asElement().screenshot({ path: `${OUT}${s.file}.png` });
      return p;
    }
  },
  {
    file: 'Invite List Phone', run: async (s) => {
      const p = await invites(375, 812);
      const r = await p.evaluate(() => {
        const sec = document.querySelector('#inviteList').closest('.base-collapsible-section').getBoundingClientRect();
        const c = document.querySelectorAll('.um-invite')[1].getBoundingClientRect();
        return { x: sec.left, y: sec.top + scrollY, w: sec.width, h: c.bottom - sec.top + 8 };
      });
      await p.screenshot({ path: `${OUT}${s.file}.png`, fullPage: true, clip: { x: r.x, y: r.y, width: r.w, height: r.h } });
      return p;
    }
  },
  { file: 'Create Invite', section: 'Create Invite', run: inviteSection },
  { file: 'Manage Access', section: 'Manage Access', run: inviteSection },
  {
    file: 'Password Resets', run: async (s) => {
      const p = await b.newPage({ viewport: { width: 1280, height: 900 }, deviceScaleFactor: 2 });
      await p.goto(ROOT + 'resets.html');
      await p.waitForFunction(() => document.querySelectorAll('#resetList .base-copy-field, #resetList input').length > 0);
      await p.waitForTimeout(600);
      await (await p.$('#UserManagementResetsForm')).screenshot({ path: `${OUT}${s.file}.png` });
      return p;
    }
  },
  {
    file: 'Invite Signup', run: async (s) => {
      const p = await b.newPage({ viewport: { width: 960, height: 760 }, deviceScaleFactor: 2, colorScheme: 'dark' });
      await p.addInitScript(() => { window.__errs = []; window.addEventListener('error', (e) => window.__errs.push(e.message)); });
      await p.goto(ROOT + 'invite/0f3c9a27b1d84e6f');
      await p.waitForSelector('#form:not(.hidden)');
      await p.waitForTimeout(400);
      await p.screenshot({ path: `${OUT}${s.file}.png` });
      return p;
    }
  },
  {
    // Jellyfin's own Activity page on the throwaway server activity.py starts, which passes it in JELLYFIN_*.
    file: 'Activity Log', server: true, run: async (s) => {
      const env = process.env;
      const p = await b.newPage({ viewport: { width: 1280, height: 800 }, deviceScaleFactor: 2, colorScheme: 'dark' });
      await p.addInitScript((c) => {
        window.__errs = [];
        localStorage.setItem('jellyfin_credentials', JSON.stringify({ Servers: [{ Id: c.server, ManualAddress: c.url, LocalAddress: c.url, UserId: c.user, AccessToken: c.token, LastConnectionMode: 2, ManualAddressOnly: true, DateLastAccessed: Date.now() }] }));
      }, { server: env.JELLYFIN_SERVER, url: env.JELLYFIN_URL, user: env.JELLYFIN_USER, token: env.JELLYFIN_TOKEN });
      await p.goto(env.JELLYFIN_URL + '/web/#/dashboard/activity');
      await p.waitForSelector('text=Invite Created', { timeout: 60000 });
      await p.waitForTimeout(1000);
      // The page's own System filter: entries no user made, which leaves out the sign-in and session rows and the
      // network address they carry.
      await p.getByRole('button', { name: 'System', exact: true }).click();
      await p.waitForSelector('text=Invite Created');
      await p.waitForTimeout(800);
      const table = await p.evaluateHandle(() => document.querySelector('.MuiTableContainer-root')?.closest('.MuiPaper-root') || document.querySelector('table'));
      await table.asElement().screenshot({ path: `${OUT}${s.file}.png` });
      return p;
    }
  },
  {
    file: 'Invite QR Code', run: async (s) => {
      const p = await invites(800, 900);
      await p.click('.um-invite:nth-child(1) .base-qr-code-open');
      await p.waitForTimeout(500);
      await (await p.$('.base-dialog-content')).screenshot({ path: `${OUT}${s.file}.png` });
      return p;
    }
  }
];

const only = process.argv.slice(2);
for (const name of only) {
  if (!shots.some((s) => s.file === name)) console.error(`No shot named ${name}.`);
}
for (const s of shots) {
  if (only.length ? !only.includes(s.file) : s.server) continue;
  if (s.server && !process.env.JELLYFIN_URL) { console.error(`${s.file} needs a server: run python3 -I activity.py.`); continue; }
  const p = await s.run(s);
  const errs = await p.evaluate(() => window.__errs.filter((e) => !/ResizeObserver/.test(e)));
  console.log(s.file, JSON.stringify(errs));
  await p.close();
}
await b.close();
