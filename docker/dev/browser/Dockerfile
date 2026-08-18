FROM mcr.microsoft.com/playwright:v1.62.1-noble

WORKDIR /work
COPY package.json package-lock.json ./
RUN npm ci --omit=dev

COPY server.js cdp-proxy.js viewer.html ./

EXPOSE 6080 9222
CMD ["node", "server.js"]
