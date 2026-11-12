---
name: DashLane
source: "Current ASP.NET Core MVC workspace"
auth:
  color-scheme: "light"
  css-properties:
    --bg: "#faf9f6"
    --bg-raised: "#ffffff"
    --surface: "#ffffff"
    --surface-2: "#f4f3ef"
    --surface-3: "#eceae4"
    --line: "rgba(20, 18, 12, 0.07)"
    --line-2: "rgba(20, 18, 12, 0.11)"
    --line-3: "rgba(20, 18, 12, 0.2)"
    --text: "#121110"
    --text-2: "#57544d"
    --text-3: "#8b877e"
    --accent: "#ffbd21"
    --accent-2: "#ffcb52"
    --accent-ink: "#1f1500"
    --accent-text: "#a85b00"
    --accent-soft: "rgba(255, 176, 0, 0.14)"
    --accent-line: "rgba(214, 140, 0, 0.45)"
    --ok: "#15803d"
    --ok-fill: "#22c55e"
    --ok-soft: "rgba(34, 197, 94, 0.12)"
    --err: "#c2410c"
    --err-soft: "rgba(234, 88, 12, 0.08)"
    --ink: "#121110"
    --asphalt: "#26252a"
    --r-xs: "6px"
    --r-sm: "8px"
    --r: "10px"
    --r-lg: "14px"
    --r-xl: "20px"
    --sans: '"Geist", ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif'
    --mono: '"Geist Mono", ui-monospace, "SF Mono", Menlo, monospace'
    --ease: "cubic-bezier(0.2, 0.8, 0.2, 1)"
    --edge: "inset 0 1px 0 0 rgba(255, 255, 255, 0.9)"
    --lift: "0 1px 2px rgba(20, 18, 12, 0.04), 0 12px 32px -16px rgba(20, 18, 12, 0.18)"
    --gutter: "clamp(16px, 4vw, 32px)"
  typography:
    body: { size: "14px", line-height: "1.5", features: '"ss01", "cv11"' }
    numeric-class: "tabular"
  layout:
    container-max: "1200px"
    topbar-height: "60px"
    topbar-mobile-height: "56px"
    topbar-mobile-breakpoint: "720px"
bootstrap-shell:
  version: "5.1.0"
  root-font-size: "14px"
  root-font-size-from-768px: "16px"
  scoped-link: "#0077cc"
  scoped-primary: "#1b6ec2"
  scoped-primary-border: "#1861ac"
  scoped-divider: "#e5e5e5"
  focus-blue: "#258cfb"
  footer-height: "60px"
ride-workflows:
  canvas: "#FBFBFA"
  card: "#FFFFFF"
  field: "#F8FAFC"
  progress-track: "#F1F5F9"
  css-properties:
    --ride-primary: "#0F766E"
    --ride-hover: "#115E59"
    --ride-tint: "#F0FDFA"
    --ride-selected: "#CCFBF1"
    --ride-border: "#E2E8F0"
    --ride-ink: "#152638"
    --ride-muted: "#475569"
    --ride-shadow: "0 1px 2px rgb(15 23 42 / .04), 0 5px 20px rgb(15 23 42 / .05)"
    --ride-shadow-raised: "0 3px 8px rgb(15 23 42 / .07), 0 12px 26px rgb(15 23 42 / .08)"
  status:
    verified: { background: "#ECFDF5", ink: "#065F46" }
    warning: { background: "#FEF3C7", ink: "#B45309" }
    danger: { background: "#FFE4E6", ink: "#BE123C" }
    rating-star: "#D97706"
  typography:
    body: '"Hanken Grotesk", system-ui, sans-serif'
    headings: "Manrope, system-ui, sans-serif"
    numbers: "tabular-nums"
  geometry:
    card: "24px"
    pill: "999px"
    columns: "minmax(0, 65fr) minmax(0, 35fr)"
    collapse-at: "991px"
---

# DashLane UI design system

## Brand & Style

Home, sign-in, sign-up, member onboarding, and public information pages use the warm Geist interface in [`_AuthLayout.cshtml`](Views/Shared/_AuthLayout.cshtml). This layout loads [`fonts.css`](wwwroot/css/fonts.css) followed by [`app.css`](wwwroot/css/app.css). Near-black carries primary actions, amber highlights the brand and selected states, and warm-white surfaces use thin translucent borders. The landing page adds the road-line animation, amber and peach auroras, translucent floating cards, and a route palette illustration.

The default [`_Layout.cshtml`](Views/Shared/_Layout.cshtml) loads local Bootstrap 5.1.0, [`site.css`](wwwroot/css/site.css), the generated scoped stylesheet from [`_Layout.cshtml.css`](Views/Shared/_Layout.cshtml.css), and optional view styles. The rider and driver workflows use this shell and layer on their own emerald styles. The CSS scope determines which palette and component classes apply. There is no Admin Razor view in the audited workspace.

## Colors

| Role | Home and accounts | Ride workflows |
|---|---|---|
| Canvas | `var(--bg)` | `#FBFBFA` |
| Raised card and input | `var(--surface)` and `var(--bg-raised)` | Cards `#FFFFFF`, fields `#F8FAFC` |
| Secondary and deeper trays | `var(--surface-2)`, `var(--surface-3)` | Progress `#F1F5F9`, soft selection `#F0FDFA` and `#CCFBF1` |
| Primary text and muted text | `var(--text)`, `var(--text-2)` | `var(--ride-ink)`, `var(--ride-muted)` |
| Placeholder and quiet text | `var(--text-3)`, except OTP placeholders use `var(--surface-3)` | Placeholder colour inherits the browser/Bootstrap control styling where no local override exists |
| Primary action and hover | `#121110` → `#2a2825`, white text | `#0F766E` → `#115E59`, white text |
| Accent action and hover | `#ffbd21` → `#ffcb52`, ink `#1f1500` | Emerald action with pale teal secondary surfaces |
| Dividers and borders | `--line` for quiet dividers, `--line-2` for cards and controls, `--line-3` for stronger and hover borders | `1px solid #E2E8F0`, selected stop border `#99E0D5`, selected tier border `#0F766E` |
| Verification | `.tag-ok` uses `--ok-soft`, `--ok`, and `rgba(34, 197, 94, 0.25)` border | Emerald pills use `#ECFDF5` with `#065F46` |
| Attention | `.tag-accent` uses `--accent-soft`, `--accent-text`, and `rgba(255, 176, 0, 0.2)` border | Surge and late driver use `#FEF3C7` with `#B45309` |
| Error and danger | `.tag-err` uses `--err-soft`, `--err`, and `rgba(234, 88, 12, 0.25)` border | Cancellation and SOS use `#FFE4E6` with `#BE123C`. Driver field errors use `#B91C1C`. |

The Bootstrap shell’s navbar uses `.navbar-light.bg-white` and `.nav-link.text-dark`. Its scoped overrides specify `#0077cc` links, `#1b6ec2` primary and active nav-pills with `#1861ac` borders, and `1px solid #e5e5e5` dividers. The focus override in `site.css` is `0 0 0 0.1rem white, 0 0 0 0.25rem #258cfb` for buttons, nav-link buttons, form controls, and checkboxes.

## Typography

The account shell self-hosts `geist-variable.woff2` and `geist-mono-variable.woff2`. Each face declares normal style, weights 100–900, and `font-display: swap`. Body copy uses `var(--sans)`, 14px, line-height 1.5, and OpenType features `"ss01", "cv11"`. `.mono` switches to `var(--mono)` and resets the features to normal. `.tabular` and `.stat-value` set `font-variant-numeric: tabular-nums`.

| Account selector | Size | Weight | Line height | Tracking |
|---|---|---|---|---|
| `.display` | `clamp(44px, 8.4vw, 104px)` | 600 | 0.95 | −0.055em |
| `.lede` | `clamp(16px, 1.6vw, 18.5px)` | inherited, emphasis 500 | 1.55 | −0.01em |
| `.auth-title` | 26px | 600 | 1.15 | −0.035em |
| `.auth-sub` | 14.5px | inherited | inherited | inherited |
| `.sheet-title` | `clamp(28px, 3.6vw, 40px)` | 600 | 1.08 | −0.045em |
| `.sheet-lede` | 15.5px | inherited | 1.55 | inherited |
| `.stat-value` | `clamp(32px, 4vw, 44px)` | 600 | 1 | −0.045em |
| `.wordmark` | 15px | 600 | inherited | −0.02em |
| `.label` / `.hint` | 13px / 12.5px | 500 / inherited | inherited / 1.45 | inherited |
| `.btn` | 14px | 500 | inherited | −0.01em |
| `.otp` | 28px Geist Mono | inherited | inherited | 0.6em, with matching text indent |

The Bootstrap shell’s root is 14px below 768px and 16px from 768px. The default layout has no Google Fonts import. Ride and driver views declare the [Hanken Grotesk and Manrope stylesheet](https://fonts.googleapis.com/css2?family=Hanken+Grotesk:wght@400;500;600;700&family=Manrope:wght@500;600;700;800&display=swap) in their own Styles sections. Hanken Grotesk weights are 400/500/600/700 and Manrope weights are 500/600/700/800. `.ride-page` applies tabular numbers throughout. Headings and legends use Manrope 800. The ride H1 uses `clamp(1.75rem, 4vw, 2.5rem)` and `-.04em` tracking. H2 and legends are `1.1rem`. Driver workflows also use `.post-tabular` for numeric fields.

## Layout & Spacing

- **Account shell**. `.main` is a growing, full-width flex column. `.container` caps at 1200px with `--gutter` horizontally. `.topbar` is sticky at `top: 0`, `z-index: 40`. `.topbar-inner` is a 1200px frame with a 60px height and 24px gap. It holds the wordmark, landing navigation, and an end group for language and account controls. Navigation links have `6px 10px` padding, an 8px radius, and a `--surface-2` hover. At 720px the bar is 56px, its gap becomes 10px, and landing navigation and the account name hide.
- **Account footer**. `.footer > .footer-inner` uses a `1px solid var(--line)` divider and 64px top margin. The inner frame caps at 1200px, uses `20px var(--gutter)` padding, 16px flex gaps, and 12.5px quiet text. `.footer-nav` wraps with 20px gaps. The inner frame stacks left-aligned at 560px.
- **Landing**. `.hero` centres content and starts at `clamp(48px, 7vw, 88px)` top padding. `.lede` caps at 560px. `.hero-actions` wraps with 10px gaps and a 32px top margin. The ticker replaces hero metadata at 1180px. At 720px hero top padding becomes 36px, `.display` becomes `clamp(42px, 13vw, 60px)`, and CTA buttons share the row. Stats reflow from four columns to two. The landing map scrolls horizontally with an 820px minimum SVG width.
- **Sign-in and sign-up**. `.auth` centres content with `clamp(32px, 7vw, 80px) var(--gutter)` padding. Sign-in uses `.auth-box` capped at 380px. Sign-up uses `.auth-split` capped at 1040px with `minmax(0, 400px) minmax(0, 1fr)` columns and `clamp(32px, 7vw, 96px)` gap. At 960px, it stacks at a 440px maximum and hides the journey illustration. `.auth-form` and `.auth-stack` use 16px and 20px vertical gaps.
- **Account setup**. `.setup` is a 1200px frame with 232px navigation and fluid content, separated by `clamp(24px, 4vw, 56px)`. `.steps` and `.preview` stick at 84px. `.sheet-grid` uses fluid content and a 300px preview with a 24px gap, then stacks at 1080px. At 960px the setup columns stack, the steps become a horizontally scrollable strip, and form sections stack. A desktop `.form-section` uses a 200px label column, a fluid control column, a 24px gap, and 28px padding. At 720px the action bar becomes sticky at the bottom. Two-column fields and selectors stack at 560px. At 380px the topbar wordmark text hides and the date-of-birth grid becomes `64px minmax(0, 1fr) 84px` with an 8px gap.
- **Public help and legal**. Help categories use four columns, two at 1000px, and one at 480px in the early rule. The later 720px override specifies two columns and an 8px gap and wins where both rules match. `.help-body` uses fluid content plus a 300px sidebar with a 40px gap and stacks at 1000px. The legal TOC uses 210px beside up to 760px of content and hides at 1000px. Desktop legal and help sidebars stick at 88px.
- **Bootstrap shell**. The header uses `.navbar.navbar-expand-sm.navbar-toggleable-sm.navbar-light.bg-white.border-bottom.box-shadow.mb-3` with `.container-fluid`, brand, collapse toggle, and `.navbar-nav.flex-grow-1`. It expands at 576px. The main uses `.container > main.pb-3`. Bootstrap container maxima are 540/720/960/1140/1320px at 576/768/992/1200/1400px. The footer uses `.border-top.footer.text-muted > .container`, is absolute at the bottom, and has 60px line-height with a matching 60px body margin.
- **Ride workflows**. `.ride-page` caps at 1200px and sits within the Bootstrap container. `.ride-grid` uses 65fr/35fr and a 1.35rem gap. Selector and aside use 1rem gaps. `.ride-aside` sticks at 1rem on desktop and becomes static in the single column at 991px. Rider tier cards stack at 420px. Driver field and tier grids stack at 900px. Driver route scheduling uses two columns before its 900px collapse.

## Elevation & Depth

| Selector | Exact treatment |
|---|---|
| `.card`, `.journey`, `.menu` | `box-shadow: var(--edge), var(--lift)` |
| `.panel` | `1px solid var(--line)` with no explicit shadow |
| `.btn-primary` | `inset 0 1px 0 rgba(255, 255, 255, 0.18), 0 1px 2px rgba(0, 0, 0, 0.2), 0 8px 20px -10px rgba(0, 0, 0, 0.5)` |
| `.btn-accent` | `inset 0 1px 0 rgba(255, 255, 255, 0.5), 0 0 0 1px rgba(196, 128, 0, 0.35), 0 10px 24px -12px rgba(230, 150, 0, 0.8)` |
| `.btn-secondary` | `var(--edge)` |
| `.topbar.is-solid`, `.topbar.is-stuck` | `rgba(250, 249, 246, 0.78)`, `saturate(180%) blur(16px)`, bottom `--line` |
| `.float-card` | `rgba(255, 255, 255, 0.82)`, white `.95` border, `0 0 0 1px rgba(20, 18, 12, 0.06), 0 18px 40px -18px rgba(80, 50, 0, 0.35)`, `blur(14px) saturate(160%)` |
| `.palette` | `rgba(255, 255, 255, 0.78)`, white `.9` border, `0 0 0 1px rgba(20, 18, 12, 0.08), 0 2px 4px rgba(20, 18, 12, 0.04), 0 40px 80px -24px rgba(60, 40, 0, 0.35)`, `blur(24px) saturate(180%)` |
| Mobile `.form-actions` | `rgba(250, 249, 246, 0.92)` with `blur(12px)` |
| Bootstrap `.box-shadow` | `0 .25rem .75rem rgba(0, 0, 0, .05)` |
| `.ride-card` and tier hover | `--ride-shadow` and `--ride-shadow-raised` respectively |
| `.checkout-fare` | `0 5px 12px rgb(15 23 42 / .07), 0 20px 48px rgb(15 23 42 / .09)` |
| `.confirm-dialog::backdrop` | `rgb(15 23 42 / .5)` |

The ambient layer uses `radial-gradient(ellipse 60% 40% at 50% -8%, rgba(255, 176, 0, 0.12), transparent 70%)` and `radial-gradient(ellipse 40% 30% at 85% 0%, rgba(255, 120, 60, 0.06), transparent 70%)` over a masked 64px grid. Landing auroras use `#ffd37a`, `#ffb38a`, and `#ffe7b0` with 70px blur. Hero lettering uses `linear-gradient(180deg, #121110 35%, #5d5a52)` with `linear-gradient(100deg, #f5a300 10%, #ea580c 90%)` for the emphasis. The setup progress fill uses `linear-gradient(90deg, var(--accent), #ff9b3d)` with `0 0 12px rgba(255, 176, 0, 0.5)`. Reduced-motion rules are in the account and workflow stylesheets.

## Shapes

Account cards and journey panels use 20px corners. Panels, options, language menus, and hero floating cards use 14px. The hero palette uses 16px. Inputs, notices, and default buttons use 10px. Small buttons and icon controls use 8px, large buttons 12px, and status tags 6px. Pills use 999px and avatars use 50%. Cards, options, and inputs use a 1px solid border.

Ride and driver cards use 24px. Rider selects and CTAs use 999px. Confirmation text fields use 14px and the confirmation select uses 999px. Driver inputs and file controls use 16px. The native ride dialog inherits the 24px card radius and 1px border, with width `min(29rem, calc(100% - 2rem))` and padding `1.25rem`.

## Components

### Home and account components

```html
<button class="btn btn-primary btn-lg btn-block" type="submit">Log in</button>
<button class="btn btn-accent btn-lg btn-block" type="submit">Create account</button>
<button class="btn btn-secondary btn-lg btn-block" type="button">Continue with Google</button>
<div class="field">
  <label class="label" for="email">Email</label>
  <div class="control">
    <input class="input" id="email" type="email" autocomplete="email">
  </div>
  <p class="hint-error" role="status" hidden>Enter a valid email address.</p>
</div>
<span class="tag tag-ok">Verified</span>
<p class="notice notice-err" role="status">Check your details and try again.</p>
```

- **Buttons**. The default button is 38px high with 16px horizontal padding, an 8px inline gap, and a transparent 1px border. `.btn-sm` uses 32px/12px/13px for height/padding/font. `.btn-lg` uses 46px/22px/15px. `.btn-block` sets 100% width. Secondary buttons hover from `--surface-2` to `--surface-3` and strengthen the border from `--line-2` to `--line-3`. Ghost buttons hover to `--surface-2`. Danger buttons use `--err` and `--err-soft` on hover. Active buttons scale to `.985`. Disabled buttons use `.5` opacity.
- **Fields and selects**. `.field > .label + .control > .input` uses an 8px field gap. Use `.select-wrap > select.select` for selects and `textarea.textarea` for multiline input. Fields are 42px tall with 12px side padding, a 10px radius, and `0 1px 2px rgba(20, 18, 12, 0.04)`. Leading control icons increase left padding to 38px. Selects reserve 36px on the right. Textareas have a 112px minimum, 12px padding, 1.55 line-height, and vertical resize. Focus uses `rgba(214, 140, 0, 0.6)` and a 3px accent-soft ring. Invalid inputs use `rgba(234, 88, 12, 0.55)` and a 3px error-soft ring. At 720px field text becomes 16px and single-line controls become 46px high.
- **Selectors and cards**. Use `.card.card-pad` for a padded card and `.options.options-2` or `.options.options-3` around `.option` with `.option-icon`, `.option-body`, `.option-title`, `.option-sub`, and `.option-mark`. The grid gap is 10px. Options use 14px padding and a 12px internal gap. Apply `.is-selected` or `aria-checked="true"` for a `--accent-line` border, `linear-gradient(180deg, rgba(255, 176, 0, 0.06), rgba(255, 176, 0, 0.02))`, and `0 0 0 3px rgba(255, 176, 0, 0.05)`.
- **Pills, badges, and alerts**. `.pill` is 28px high with 12.5px text, a 999px radius, and `rgba(255, 255, 255, 0.7)` background. `.tag` is 22px high with 12px/500 text and 8px horizontal padding. Use `.tag-ok`, `.tag-accent`, and `.tag-err` for status. `.notice` has `10px 12px` padding, 13.5px text, and a 6px leading dot. `.notice-ok` and `.notice-err` apply the corresponding soft surface, border, and dot colour while retaining primary body ink. The landing `.palette` is an illustration within the hero stage.

### Bootstrap and workflow components

```html
<div class="ride-card confirm-panel">
  <label for="pickup-notes">Pickup instructions</label>
  <textarea class="confirm-field" id="pickup-notes"></textarea>
  <span class="ride-chip">Route preview</span>
  <button class="ride-primary" type="submit">Continue</button>
</div>
<dialog class="ride-card confirm-dialog" aria-labelledby="dialog-title">
  <h2 id="dialog-title">Cancel ride</h2>
  <button class="confirm-button trip-rose" type="button">Confirm cancellation</button>
</dialog>
```

- **Actions**. `.ride-primary` fills its container, has a 2.8rem minimum height, `.55rem .85rem` padding, and weight 800. Disabled uses `#E2E8F0` with `#475569` text. `.confirm-button` uses a white surface, a 1px border, teal text, and `.4rem .7rem` padding, with a teal tint on hover. `.ride-round` provides 2.25rem circular icon actions.
- **Inputs and selectors**. Route fields use `.ride-stop > select`. Valid selections use the pale teal surface. `.ride-card.ride-tier` contains a native radio and uses `:has(input:checked)` to apply the teal tint, border, and 1px focus-like edge. Confirmation and checkout use `.confirm-field`. Driver forms use `.post-field > .post-input` or `.post-file`, with a 2.85rem minimum height and a 3px `rgb(15 118 110 / .15)` focus ring.
- **Trust, alerts, and modal**. Use `.confirm-pills` for driver verification, `.checkout-lock` for the price-lock badge, `.ride-surge` or `.trip-amber` for warnings, and `.trip-rose` for cancellation and SOS. Checkout success/error text uses `#065F46` / `#BE123C`. The native `dialog.ride-card.confirm-dialog` pattern is used for notes, chat, cancellation, SOS, and incident reports.
- **Style sources**. Shared workflow rules are in [`ride-request.css`](wwwroot/css/ride-request.css). Page details are in [`ride-confirm.css`](wwwroot/css/ride-confirm.css), [`ride-checkout.css`](wwwroot/css/ride-checkout.css), [`ride-status.css`](wwwroot/css/ride-status.css), [`driver-onboarding.css`](wwwroot/css/driver-onboarding.css), and [`driver-post-route.css`](wwwroot/css/driver-post-route.css).
