import { t } from "./index";

describe("i18n t()", () => {
  it("resolves a message by selector", () => {
    expect(t((m) => m.nav.dashboard)).toBe("Dashboard");
  });

  it("fills interpolation tokens", () => {
    expect(t((m) => m.theme.toggle, { current: "Light", next: "Dark" })).toBe(
      "Theme: Light. Switch to Dark.",
    );
    expect(t((m) => m.common.pageAnnouncement, { title: "Progress" })).toBe("Progress page");
  });

  it("leaves unknown tokens untouched and ignores extra vars", () => {
    expect(t((m) => m.common.pageChanged, { unused: "x" })).toBe("Page changed");
  });
});
