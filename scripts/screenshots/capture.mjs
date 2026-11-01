// Captures the README screenshots from a running stack (docker compose up).
// Usage: node scripts/screenshots/capture.mjs [outputDir]
// Env: UI_URL (default http://localhost:8081), API_URL (default http://localhost:8080), DEMO_PASSWORD.
//
// Each role gets its own browser context and signs in through the real login form,
// so the pictures show exactly what a reviewer sees after "docker compose up".

import { chromium } from 'playwright';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

const UI = (process.env.UI_URL ?? 'http://localhost:8081').replace(/\/$/, '');
const API = (process.env.API_URL ?? 'http://localhost:8080').replace(/\/$/, '');
const PASSWORD = process.env.DEMO_PASSWORD ?? 'Demo@12345';
const OUT = process.argv[2] ?? 'docs/screenshots';
const VIEWPORT = { width: 1366, height: 900 };

/** The product with the most offers, so the product page shows the multi-seller comparison. */
async function productWithMostOffers() {
  const response = await fetch(`${API}/api/v1/products?pageSize=100&sort=price`);
  if (!response.ok) throw new Error(`Product search failed: ${response.status}`);
  const { items } = await response.json();
  return items.reduce((best, item) => (item.offerCount > best.offerCount ? item : best));
}

async function shot(page, name, url, options = {}) {
  await page.goto(`${UI}${url}`, { waitUntil: 'networkidle' });
  if (options.before) await options.before(page);
  await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: options.fullPage ?? false });
  console.log(`saved ${name}.png  <-  ${url}`);
}

/** Opens a fresh context, signs in with the login form, runs the captures, closes the context. */
async function asUser(browser, email, captures) {
  const context = await browser.newContext({ viewport: VIEWPORT });
  const page = await context.newPage();
  if (email) {
    await page.goto(`${UI}/Account/Login`, { waitUntil: 'networkidle' });
    await page.fill('#Email', email);
    await page.fill('#Password', PASSWORD);
    await Promise.all([page.waitForURL((url) => !url.pathname.toLowerCase().includes('/account/login')), page.click('form button[type=submit]')]);
  }
  await captures(page);
  await context.close();
}

/** First link on the current page whose path matches, e.g. the newest order in a list. */
async function firstLink(page, pattern) {
  const hrefs = await page.$$eval('a[href]', (links) => links.map((a) => a.getAttribute('href')));
  const href = hrefs.find((h) => pattern.test(h ?? ''));
  if (!href) throw new Error(`No link matching ${pattern} on ${page.url()}`);
  return href;
}

await mkdir(OUT, { recursive: true });
const product = await productWithMostOffers();
const browser = await chromium.launch();

try {
  await asUser(browser, null, async (page) => {
    await shot(page, '01-home', '/');
    await shot(page, '02-catalog-filters', '/catalog?category=ram&memory-type=ddr5');
    await shot(page, '03-product-offers', `/products/${product.slug}`, { fullPage: true });
    await shot(page, '04-login-demo-accounts', '/Account/Login');
  });

  await asUser(browser, 'customer@pcm.local', async (page) => {
    await shot(page, '05-cart', '/cart');
    await shot(page, '06-checkout', '/checkout');
    await shot(page, '07-orders', '/orders');
    await page.goto(`${UI}/orders`, { waitUntil: 'networkidle' });
    await shot(page, '08-order-split-by-seller', await firstLink(page, /^\/orders\/\d+$/), { fullPage: true });
  });

  await asUser(browser, 'seller1@pcm.local', async (page) => {
    await shot(page, '09-seller-dashboard', '/seller');
    await shot(page, '10-seller-offers', '/seller/offers');
    await shot(page, '11-seller-orders', '/seller/orders');
  });

  await asUser(browser, 'admin@pcm.local', async (page) => {
    await shot(page, '12-admin-dashboard', '/admin');
    await shot(page, '13-admin-sellers', '/admin/sellers');
    await shot(page, '14-admin-review-queue', '/admin/products?status=Pending');
    await shot(page, '15-admin-categories', '/admin/categories');
  });

  // API docs (Scalar renders client-side: give it a moment after the network settles).
  await asUser(browser, null, async (page) => {
    await page.goto(`${API}/scalar`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(1500);
    await page.screenshot({ path: path.join(OUT, '16-api-scalar.png') });
    console.log('saved 16-api-scalar.png  <-  /scalar');
  });
} finally {
  await browser.close();
}
