import {defineConfig} from "@playwright/test";
import path from "node:path";
import base from "./playwright.config";
const evidence=path.resolve(__dirname,"Tools/proposals/hitos-04-05/evidencias");
export default defineConfig({
  ...base,
  testDir:"./tests/browser/fourth-milestone",
  outputDir:path.join(evidence,"resultados"),
  reporter:[["list"],["json",{outputFile:path.join(evidence,"playwright-results.json")}]],
});
