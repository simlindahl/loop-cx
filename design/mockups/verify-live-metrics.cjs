const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const assert = require("node:assert/strict");
const path = require("node:path");
const fs = require("node:fs");
const { pathToFileURL } = require("node:url");

(async () => {
  const browser = await chromium.launch({ channel: process.env.PLAYWRIGHT_CHANNEL || "chrome", headless: true });
  const page = await browser.newPage({ viewport: { width: 1200, height: 1100 }, reducedMotion: "no-preference" });
  let checks = 0;
  const check = (name, value) => {
    assert.ok(value, name);
    checks++;
  };
  const state = () => page.evaluate(() => window.trendState());

  try {
    await page.goto(pathToFileURL(path.join(__dirname, "live-metrics/index.html")).href);
    await page.evaluate(() => document.fonts.ready);
    check("Shared local font loaded", await page.evaluate(() => document.fonts.check('16px "Space Grotesk"')));

    // Optional transfer proof against the exact local approved study, never a maintained dependency.
    if (process.env.APPROVED_STUDY) {
      const original = await browser.newPage();
      await original.goto(pathToFileURL(process.env.APPROVED_STUDY).href);
      const phone = p => p.locator(".phone").evaluate(e => e.outerHTML);
      check("Reviewed phone DOM preserved", await phone(page) === await phone(original));
      const css = async (p, directory) => p.evaluate(text => {
        const style = document.createElement("style");
        style.textContent = text;
        document.head.append(style);
        const rules = Array.from(style.sheet.cssRules, rule => rule.cssText);
        style.remove();

        return rules;
      }, fs.readFileSync(path.join(directory, "style.css"), "utf8"));
      const originalDirectory = path.dirname(process.env.APPROVED_STUDY);
      check("Reviewed CSSOM preserved", JSON.stringify(await css(page, path.join(__dirname, "live-metrics"))) === JSON.stringify(await css(original, originalDirectory)));
      check("Reviewed script preserved", fs.readFileSync(path.join(__dirname, "live-metrics/prototype.js"), "utf8").trimEnd() === fs.readFileSync(path.join(originalDirectory, "prototype.js"), "utf8").trimEnd());
      await original.close();
    }

    await page.locator('[data-pace="fast240"]').click();
    check("Pending preference stops arrow", (await state()).arrowAnimations === 0);
    await page.locator("#ready").click();

    for (const [scenario, duration, label] of [["fast240", 960, "Faster"], ["fast320", 1280, "Faster"], ["slow420", 1680, "Slower"]]) {
      await page.locator(`[data-pace="${scenario}"]`).click();
      const actual = await state();
      check(`Synthetic mapping ${scenario}`, actual.duration === duration && actual.label === label && actual.arrowAnimations === 1);
    }

    check("Bounded transform-only repeating arrow", await page.evaluate(() => {
      const effect = document.getAnimations().find(a => a.effect.target.id === "trend-arrow").effect;

      return effect.getTiming().iterations === Infinity && effect.getKeyframes().every(k => !("opacity" in k) && !("color" in k)) && effect.getKeyframes().some(k => k.transform.replaceAll(" ", "") === "translate(2px,3px)");
    }));
    const digits = await page.locator("#pace-value").boundingBox();
    await page.evaluate(() => document.getAnimations().find(a => a.effect.target.id === "trend-arrow").currentTime = 840);
    check("Pace digit geometry stable", JSON.stringify(digits) === JSON.stringify(await page.locator("#pace-value").boundingBox()));
    await page.locator('[data-pace="steady"]').click();
    check("Steady static", (await state()).label === "Steady" && (await state()).arrowAnimations === 0);

    for (const scenario of ["stale", "gap", "phase", "acquiring"]) {
      await page.locator('[data-pace="fast240"]').click();
      await page.locator(`[data-pace="${scenario}"]`).click();
      check(`${scenario} stops arrow`, (await state()).arrowAnimations === 0);
    }

    for (const pause of ["manual", "auto"]) {
      await page.locator("#pause").selectOption("running");
      await page.locator('[data-pace="fast240"]').click();
      await page.locator("#pause").selectOption(pause);
      check(`${pause} stops pace but fresh HR remains symbolic`, (await state()).previousWindow === null && (await state()).arrowAnimations === 0 && await page.locator("#pace-value").textContent() === "Unavailable" && (await page.evaluate(() => window.previewState())).duration === 60000 / 156);
    }

    for (const quality of ["stale", "contact-invalid", "unknown"]) {
      await page.locator("#quality").selectOption(quality);
      check(`${quality} HR unavailable`, await page.locator("#bpm-value").textContent() === "Unavailable" && (await page.evaluate(() => window.previewState())).duration === null);
    }

    await page.locator("#quality").selectOption("valid");
    await page.locator("#pause").selectOption("running");
    await page.locator('[data-pace="fast320"]').click();
    await page.locator("#background").check();
    check("Hidden preview stops effects", (await page.evaluate(() => window.previewState())).animations === 0);
    await page.locator("#background").uncheck();
    await page.emulateMedia({ reducedMotion: "reduce" });
    await page.waitForFunction(() => window.previewState().animations === 0);
    check("OS reduced motion retains static direction", (await state()).label === "Faster" && (await state()).arrowAnimations === 0);
    await page.setViewportSize({ width: 390, height: 1000 });
    await page.evaluate(() => document.documentElement.style.fontSize = "200%");
    check("200% phone reflow", await page.locator(".phone").evaluate(e => e.scrollWidth <= e.clientWidth));
    console.log(`${checks} focused live-measurement reference checks passed. Synthetic browser evidence only; parameters remain uncalibrated proposals.`);
  } finally {
    await browser.close();
  }
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
