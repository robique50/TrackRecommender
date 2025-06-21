const { env } = require("process");

const target = env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
  : env.ASPNETCORE_URLS
  ? env.ASPNETCORE_URLS.split(";")[0]
  : "https://localhost:7048";

const PROXY_CONFIG = [
  {
    context: [
      "/api/auth",
      "/api/user",
      "/api/userpreferences",
      "/api/mapdata",
      "/api/trails",
      "/api/reviews",
      "/api/weather",
      "/api/regions",
      "/api/trailmarkings",
      "/api/trailrecommendation",
    ],
    target: "https://localhost:7219",
    secure: false,
    changeOrigin: true,
    logLevel: "debug",
  },
];

module.exports = PROXY_CONFIG;
