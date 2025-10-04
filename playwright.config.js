// Root-level config that proxies to the integration-tests setup
delete require.cache[require.resolve('./test/integration-tests/playwright.config.js')];
module.exports = require('./test/integration-tests/playwright.config.js');
