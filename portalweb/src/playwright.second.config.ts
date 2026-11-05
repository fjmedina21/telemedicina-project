import {defineConfig} from "@playwright/test";
import path from "node:path";
import base from "./playwright.config";

const artifacts=path.resolve(__dirname,"../../hitos/portalweb/segundo-hito-portalweb/evidencias/playwright");
export default defineConfig({
  ...base,
  testMatch:"**/second-milestone/*.spec.ts",
  outputDir:path.join(artifacts,"resultados"),
  reporter:[["list"],["json",{outputFile:path.join(artifacts,"playwright-results.json")}]],
  use:{...base.use,trace:"off",video:"off",screenshot:"off"},
});
