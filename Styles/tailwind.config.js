/** Local Tailwind build for the Stitch signup and onboarding screens. */
module.exports = {
  important: ".flow-page",
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
  theme: {
  "extend": {
    "colors": {
      "secondary": "#006a61",
      "error": "#ba1a1a",
      "on-secondary-fixed": "#00201d",
      "inverse-surface": "#213145",
      "background": "#f8f9ff",
      "secondary-fixed": "#89f5e7",
      "on-tertiary-fixed": "#00201c",
      "on-tertiary-fixed-variant": "#005048",
      "on-primary-container": "#a3faef",
      "outline": "#6e7977",
      "on-error-container": "#93000a",
      "on-secondary": "#ffffff",
      "on-error": "#ffffff",
      "on-primary-fixed": "#00201d",
      "surface-container-low": "#eff4ff",
      "surface-tint": "#006a63",
      "outline-variant": "#bdc9c6",
      "primary-fixed-dim": "#80d5cb",
      "tertiary": "#005c52",
      "surface-container-lowest": "#ffffff",
      "tertiary-container": "#00776b",
      "on-primary-fixed-variant": "#00504a",
      "on-surface-variant": "#3e4947",
      "on-surface": "#0b1c30",
      "error-container": "#ffdad6",
      "primary-fixed": "#9cf2e8",
      "surface-variant": "#d3e4fe",
      "on-tertiary": "#ffffff",
      "secondary-container": "#86f2e4",
      "tertiary-fixed": "#71f8e4",
      "on-secondary-fixed-variant": "#005049",
      "on-secondary-container": "#006f66",
      "on-primary": "#ffffff",
      "on-tertiary-container": "#84ffec",
      "surface-container-high": "#dce9ff",
      "surface-container": "#e5eeff",
      "tertiary-fixed-dim": "#4fdbc8",
      "surface-dim": "#cbdbf5",
      "primary-container": "#0f766e",
      "secondary-fixed-dim": "#6bd8cb",
      "surface-bright": "#f8f9ff",
      "inverse-primary": "#80d5cb",
      "surface-container-highest": "#d3e4fe",
      "on-background": "#0b1c30",
      "inverse-on-surface": "#eaf1ff",
      "surface": "#f8f9ff",
      "primary": "#005c55"
    },
    "borderRadius": {
      "DEFAULT": "1rem",
      "lg": "2rem",
      "xl": "3rem",
      "full": "9999px"
    },
    "spacing": {
      "space-lg": "1.5rem",
      "space-sm": "0.75rem",
      "gutter": "1.5rem",
      "space-2xl": "3rem",
      "margin": "2rem",
      "space-xs": "0.5rem",
      "gutter-sm": "1rem",
      "space-xl": "2rem",
      "margin-sm": "1rem",
      "space-2xs": "0.25rem",
      "space-md": "1rem"
    },
    "fontFamily": {
      "body-sm": [
        "Hanken Grotesk"
      ],
      "caption": [
        "Hanken Grotesk"
      ],
      "headline-lg": [
        "Manrope"
      ],
      "label-numeric": [
        "Hanken Grotesk"
      ],
      "label-badge": [
        "Hanken Grotesk"
      ],
      "body-lg": [
        "Hanken Grotesk"
      ],
      "display-hero-mobile": [
        "Manrope"
      ],
      "headline-md": [
        "Manrope"
      ],
      "display-hero": [
        "Manrope"
      ],
      "headline-sm": [
        "Manrope"
      ],
      "body-md": [
        "Hanken Grotesk"
      ]
    },
    "fontSize": {
      "body-sm": [
        "13px",
        {
          "lineHeight": "18px",
          "fontWeight": "400"
        }
      ],
      "caption": [
        "11px",
        {
          "lineHeight": "14px",
          "letterSpacing": "0.03em",
          "fontWeight": "500"
        }
      ],
      "headline-lg": [
        "28px",
        {
          "lineHeight": "36px",
          "letterSpacing": "-0.02em",
          "fontWeight": "600"
        }
      ],
      "label-numeric": [
        "15px",
        {
          "lineHeight": "20px",
          "letterSpacing": "0.01em",
          "fontWeight": "600"
        }
      ],
      "label-badge": [
        "12px",
        {
          "lineHeight": "16px",
          "letterSpacing": "0.02em",
          "fontWeight": "600"
        }
      ],
      "body-lg": [
        "18px",
        {
          "lineHeight": "28px",
          "fontWeight": "400"
        }
      ],
      "display-hero-mobile": [
        "32px",
        {
          "lineHeight": "38px",
          "letterSpacing": "-0.02em",
          "fontWeight": "700"
        }
      ],
      "headline-md": [
        "22px",
        {
          "lineHeight": "28px",
          "letterSpacing": "-0.015em",
          "fontWeight": "600"
        }
      ],
      "display-hero": [
        "44px",
        {
          "lineHeight": "52px",
          "letterSpacing": "-0.03em",
          "fontWeight": "700"
        }
      ],
      "headline-sm": [
        "18px",
        {
          "lineHeight": "24px",
          "letterSpacing": "-0.01em",
          "fontWeight": "600"
        }
      ],
      "body-md": [
        "15px",
        {
          "lineHeight": "22px",
          "fontWeight": "400"
        }
      ]
    },
    "scale": {
      "98": "0.98"
    }
  }
}
};
