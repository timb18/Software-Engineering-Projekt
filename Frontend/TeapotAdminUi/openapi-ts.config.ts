import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input:
    "https://software-engineering-projekt-production.up.railway.app/swagger/v1/swagger.json",
  output: {
    path: "./src/client",
    postProcess: ["prettier", "eslint"],
  },
  parser: {
    filters: {
      operations: {
        include: ["POST /api/Organization"],
      },
    },
  },
});
