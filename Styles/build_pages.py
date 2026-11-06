#!/usr/bin/env python3
"""Generate Razor views from the Stitch export. The export itself is not modified."""
import json
import re
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXPORT = ROOT / "stitch_dashlane_ride_sharing_frontend"
VIEWS = ROOT / "Views" / "Home"
IMAGES = ROOT / "wwwroot" / "images" / "avatars"

PAGES = [
    ("create_account_dashlane", "SignUp", "Create account - DashLane", False),
    ("community_introduction_dashlane", "Community", "Getting started - DashLane", True),
    ("onboarding_step_1_about_you_dashlane", "AboutYou", "About you - DashLane", False),
    ("onboarding_step_2_profile_bio_dashlane", "Profile", "Profile - DashLane", False),
    ("onboarding_step_3_contact_verification_dashlane", "Contact", "Contact verification - DashLane", False),
    ("onboarding_step_4_identity_verification_dashlane", "Identity", "Identity verification - DashLane", False),
    ("onboarding_step_5_review_status_dashlane", "Review", "Review - DashLane", False),
]

IMAGE_NAMES = {
    "AB6AXuAYUwD621jkyMlP4PhcrErny": "signup-marcus.jpg",
    "AB6AXuB6q2UrlXc5x9r8TKCTIuCDW": "community-rider.jpg",
    "AB6AXuBCbIWar5ZLWOqd6IsVSznsQ": "sarah-k.jpg",
    "AB6AXuBOb1zpTS9NrSTwPN9ytnh5N": "profile-avatar.jpg",
    "AB6AXuAsM2TT5lexzqaAv7WldO976": "profile-preview.jpg",
    "AB6AXuAylAM6VXQjw613O78C3Jlfe": "passport-sample.jpg",
    "AB6AXuAaxQ8uW6tCuqlQGlB2QFjmL": "highway-car.jpg",
    "AB6AXuCgyMT_8gBkJQEUa7UsdbAQP": "elena-rostova.jpg",
}


def flow(action):
    return "data-flow='@Url.Action(\"%s\", \"Home\")'" % action


def add_attr_at(html, needle, attr, occurrence=0):
    start = 0
    pos = -1
    for _ in range(occurrence + 1):
        pos = html.find(needle, start)
        if pos < 0:
            raise SystemExit("missing %r #%s" % (needle, occurrence))
        start = pos + 1
    btn = html.rfind("<button", 0, pos)
    if btn < 0:
        raise SystemExit("no button before %r" % needle)
    gt = html.find(">", btn)
    name = attr.split("=")[0]
    if name in html[btn:gt]:
        return html
    return html[:gt] + " " + attr + html[gt:]


def convert_hash_links(html, status_id):
    def repl(match):
        attrs = match.group(1)
        inner = match.group(2)
        if 'href="#"' not in attrs:
            return match.group(0)
        attrs = attrs.replace('href="#"', "")
        return (
            '<button type="button" data-unavailable-message="This page is not available yet." '
            'data-status-target="%s" data-stop-label%s>%s</button>'
            % (status_id, attrs, inner)
        )

    return re.sub(r"<a\b([^>]*)>(.*?)</a>", repl, html, flags=re.S)


def unavailable(message):
    return (
        'data-unavailable-message="%s" data-status-target="flow-status"'
        % message
    )


def extract_main(text):
    match = re.search(r"<main[^>]*>(.*)</main>", text, re.S)
    body = match.group(1)
    return re.sub(r"<script[\s\S]*?</script>", "", body)


def download_images(html):
    def repl(match):
        url = match.group(1)
        name = None
        for key, filename in IMAGE_NAMES.items():
            if key in url:
                name = filename
                break
        if not name:
            raise SystemExit("unmapped image " + url[:80])
        destination = IMAGES / name
        if not destination.exists() or destination.stat().st_size < 1000:
            print("downloading", name)
            urllib.request.urlretrieve(url, destination)
        return 'src="~/images/avatars/%s"' % name

    return re.sub(r'src="(https://lh3\.googleusercontent\.com[^"]+)"', repl, html)


def build_tailwind_config():
    sample = (EXPORT / "create_account_dashlane" / "code.html").read_text()
    match = re.search(r'tailwind\.config=(\{.*?\});</script>', sample)
    config = json.loads(match.group(1))
    config["theme"]["extend"]["scale"] = {"98": "0.98"}
    rendered = json.dumps(config["theme"], indent=2)
    text = """/** Local Tailwind build for the Stitch signup and onboarding screens. */
module.exports = {
  corePlugins: { preflight: false },
  content: ["./Views/Home/**/*.cshtml", "./wwwroot/js/flow.js"],
  safelist: [
    "bg-secondary-fixed/50",
    "text-secondary",
    "bg-surface-container",
    "text-on-surface-variant",
    "bg-secondary",
    "text-on-secondary",
    "bg-surface-container-low",
    "bg-surface-container-high",
    "shadow-sm",
    "text-primary",
    "text-outline-variant",
    "opacity-90",
    "italic",
    "animate-spin",
    "animate-pulse"
  ],
  theme: %s
};
""" % rendered
    (ROOT / "Styles" / "tailwind.config.js").write_text(text)


def main():
    IMAGES.mkdir(parents=True, exist_ok=True)
    build_tailwind_config()
    for folder, view, title, center in PAGES:
        html = extract_main((EXPORT / folder / "code.html").read_text())
        html = download_images(html)
        html = html.replace('onclick="selectGender(this)"', "")
        html = html.replace(' onsubmit="event.preventDefault();"', "")

        if view == "SignUp":
            html = html.replace(
                'id="signup-form"',
                'id="signup-form" method="post" novalidate autocomplete="off"',
                1,
            )
            html = html.replace(
                '<button class="w-full h-12 rounded-full bg-surface-container-low hover:bg-surface-container transition-all flex items-center justify-center gap-space-sm px-space-md group" type="button">',
                '<button class="w-full h-12 rounded-full bg-surface-container-low hover:bg-surface-container transition-all flex items-center justify-center gap-space-sm px-space-md group" id="google-sign-up" type="button">',
                1,
            )
            html = html.replace(
                'id="password" placeholder="••••••••••••" required="" type="password"',
                'id="password" autocomplete="off" placeholder="••••••••••••" required="" type="password"',
                1,
            )
            html = html.replace(
                'aria-label="Toggle password visibility"',
                'aria-label="Show password" aria-pressed="false"',
                1,
            )
        if view == "Community":
            html = html.replace(
                'class="flex flex-col w-full items-center justify-center py-space-md"',
                'class="flex flex-col w-full items-center justify-center py-space-md my-auto"',
                1,
            )
        if view == "Profile":
            html = add_attr_at(html, 'aria-label="Quick camera shot"', 'data-photo-pick="photoUploadInput"')
            html = add_attr_at(html, ">Take photo", 'data-photo-pick="photoUploadInput"')
            html = add_attr_at(
                html,
                'id="addLangBtn"',
                unavailable("Adding more languages is not available yet."),
            )
        if view == "Contact":
            html = add_attr_at(html, ">Change number", unavailable("Changing this number is not available yet."))
            html = add_attr_at(html, ">WhatsApp", unavailable("WhatsApp delivery is not available yet."))
            html = add_attr_at(html, ">Voice Call", unavailable("Voice delivery is not available yet."))
        if view == "Identity":
            html = add_attr_at(html, ">Replace file", unavailable("Replacing this file is not available yet."))
            html = add_attr_at(html, ">Remove", unavailable("Removing this file is not available yet."))
            html = add_attr_at(html, ">Chat Now", unavailable("Live chat is not available yet."))
        if view == "Review":
            html = html.replace('id="finishBtn"', 'id="finishBtn" type="button"', 1)

        html = html.replace("@", "@@")

        if view == "SignUp":
            html = html.replace(
                'id="signup-form" method="post" novalidate autocomplete="off"',
                'id="signup-form" method="post" action="@Url.Action("SignUp", "Home")" novalidate autocomplete="off" data-next="@Url.Action("Community", "Home")"',
                1,
            )
            html = html.replace(
                'id="signup-form" method="post" action="@Url.Action("SignUp", "Home")" novalidate autocomplete="off" data-next="@Url.Action("Community", "Home")">',
                'id="signup-form" method="post" action="@Url.Action("SignUp", "Home")" novalidate autocomplete="off" data-next="@Url.Action("Community", "Home")">\n@Html.AntiForgeryToken()',
                1,
            )
            html = html.replace(
                "<!-- Primary Action Button -->",
                '<p id="signup-status" class="font-body-sm text-body-sm text-error" role="status" aria-live="polite" @(string.IsNullOrEmpty(authNotice) ? "hidden" : null)>@authNotice</p>\n<!-- Primary Action Button -->',
                1,
            )
            html = html.replace(
                '<a class="text-primary-container font-semibold hover:underline inline-flex items-center gap-0.5" href="#">',
                '<a class="text-primary-container font-semibold hover:underline inline-flex items-center gap-0.5" href="@Url.Action("SignIn", "Home")">',
                1,
            )
            status_id = "signup-status"
        else:
            status_id = "flow-status"

        if view == "Community":
            html = html.replace(
                'id="btn-complete-profile"',
                'id="btn-complete-profile" ' + flow("AboutYou"),
                1,
            )
        if view == "AboutYou":
            html = add_attr_at(html, ">Back", flow("Community"))
            html = add_attr_at(html, ">Continue to Profile", flow("Profile"))
        if view == "Profile":
            html = add_attr_at(html, ">Back", flow("AboutYou"))
            html = add_attr_at(html, ">Continue to Contact Verification", flow("Contact"))
        if view == "Contact":
            html = add_attr_at(html, ">Back", flow("Profile"))
            html = add_attr_at(html, ">Verify &amp; Continue", flow("Identity"))
        if view == "Identity":
            html = add_attr_at(html, ">Back to Contact Info", flow("Contact"))
            html = html.replace('id="submitBtn"', 'id="submitBtn" ' + flow("Review"), 1)
        if view == "Review":
            html = add_attr_at(html, ">edit</span>", flow("AboutYou"), 0)
            html = add_attr_at(html, ">edit</span>", flow("Profile"), 1)
            html = add_attr_at(html, ">edit</span>", flow("Contact"), 2)
            html = add_attr_at(html, ">upload_file</span>", flow("Identity"))
            html = html.replace(
                'id="finishBtn" type="button">',
                'id="finishBtn" type="button">',
                1,
            )
            pos = html.find('id="finishBtn"')
            close = html.find("</button>", pos)
            html = (
                html[: close + len("</button>")]
                + '\n<p id="review-status" class="font-body-sm text-body-sm text-error w-full" role="status" aria-live="polite" hidden></p>'
                + html[close + len("</button>") :]
            )

        html = convert_hash_links(html, status_id)
        if 'href="#"' in html:
            raise SystemExit("hash link remains in " + view)

        notice = ""
        if view == "SignUp":
            notice = "    var authNotice = ViewData[\"AuthNotice\"] as string;\n"
        center_class = " my-auto" if center else ""
        document = (
            "@{\n"
            '    ViewData["Title"] = "%s";\n'
            '    Layout = "_AuthLayout";\n'
            '    ViewData["MainClass"] = "flow-main";\n'
            "%s"
            "}\n\n"
            '<div class="flow-page%s">\n'
            '<p id="flow-status" class="flow-status" role="status" aria-live="polite" hidden></p>\n'
            "%s"
            "</div>\n"
        ) % (title, notice, center_class, html)
        (VIEWS / (view + ".cshtml")).write_text(document)
        print("wrote", view, "chars", len(document))


if __name__ == "__main__":
    main()
