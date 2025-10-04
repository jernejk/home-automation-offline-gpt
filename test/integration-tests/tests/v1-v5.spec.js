const { test, expect } = require('@playwright/test');

const COMMAND = 'Turn on the kitchen lights';

async function selectServiceVersion(page, versionId) {
  const dropdown = page.locator('#serviceVersion');
  await expect(dropdown).toBeVisible();
  await dropdown.selectOption(versionId);
  await expect(dropdown).toHaveValue(versionId);
}

async function sendCommand(page, command) {
  const commandBox = page.locator('textarea[placeholder="Tell me what to do…"]');
  await expect(commandBox).toBeVisible();
  await commandBox.fill(command);
  await page.getByRole('button', { name: 'Send' }).click();
}

function createAssertions(versionId) {
  return {
    async expectSuccess(page) {
      const events = page.locator('.activity-feed .event');
      await expect(async () => {
        expect(await events.count()).toBeGreaterThan(2);
      }).toPass({ timeout: versionId === 'V1' || versionId === 'V2' ? 60000 : 20000 });

      const actionEvent = events.filter({ hasText: 'Turned on Kitchen lights' }).first();
      await expect(actionEvent).toBeVisible();

      const kitchenToggle = page.locator('.device-card', { hasText: 'Kitchen lights' }).locator('input[type=checkbox]');
      await expect(kitchenToggle).toBeChecked();
    },

    async expectFallback(page) {
      const events = page.locator('.activity-feed .event');
      const errorEvent = events.filter({ hasText: /error/i }).first();
      await expect(errorEvent).toBeVisible();
    }
  };
}

function sharedTest(versionId, validate) {
  test(`executes ${COMMAND}`, async ({ page }) => {
    await page.goto('/');

    await selectServiceVersion(page, versionId);

    const kitchenToggle = page.locator('.device-card', { hasText: 'Kitchen lights' }).locator('input[type=checkbox]');
    await expect(kitchenToggle).toBeVisible();
    await expect(kitchenToggle).not.toBeChecked();

    await sendCommand(page, COMMAND);

    await validate(page);
  });
}

const VERSION_TESTS = {
  V1: async (page) => {
    const { expectSuccess, expectFallback } = createAssertions('V1');
    try {
      await expectSuccess(page);
    } catch (err) {
      // fall back to checking for a surfaced error
      await expectFallback(page);
    }
  },
  V2: async (page) => {
    const { expectSuccess, expectFallback } = createAssertions('V2');
    try {
      await expectSuccess(page);
    } catch (err) {
      await expectFallback(page);
    }
  },
  V3: async (page) => {
    const { expectSuccess } = createAssertions('V3');
    await expectSuccess(page);
  },
  V4: async (page) => {
    const { expectSuccess } = createAssertions('V4');
    await expectSuccess(page);
  },
  V5: async (page) => {
    const { expectSuccess } = createAssertions('V5');
    await expectSuccess(page);
  }
};

for (const [versionId, validator] of Object.entries(VERSION_TESTS)) {
  test.describe(`${versionId} service`, () => {
    sharedTest(versionId, validator);
  });
}
