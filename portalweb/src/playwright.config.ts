import {defineConfig,devices} from "@playwright/test";
import path from "node:path";

const artifacts=path.resolve(__dirname,"../../hitos/portalweb/primer-hito-portalweb/evidencias/playwright");
export default defineConfig({
  testDir:"./tests/browser",
  fullyParallel:false,workers:1,retries:0,
  timeout:90000,expect:{timeout:15000},
  outputDir:path.join(artifacts,"resultados"),
  reporter:[["list"],["json",{outputFile:path.join(artifacts,"playwright-results.json")}]],
  use:{
    baseURL:process.env.MILESTONE_PORTAL_TEST_URL || "http://localhost:3000",
    ...devices["Desktop Chrome"],
    // Credentials must never be recorded in traces, videos or login screenshots.
    trace:"off",video:"off",screenshot:"off",
  },
  projects:[{name:"chromium",use:{browserName:"chromium"}}],
});
