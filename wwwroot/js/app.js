(function () {
  "use strict";

  var strings = window.DL_TEXT || {};
  function t(text) { return strings[text] || text; }

  var GOOGLE_MESSAGE = t("Google sign-in isn't configured for this app yet.");
  var emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  function $(id) { return document.getElementById(id); }
  function all(selector, root) { return Array.prototype.slice.call((root || document).querySelectorAll(selector)); }

  function showStatus(target, message) {
    if (!target) return;
    target.textContent = message || "";
    target.hidden = !message;
  }

  /* ---------- First-visit intro (landing page only) ---------- */

  var root = document.documentElement;
  if (root.classList.contains("intro-on") && $("intro")) {
    var finished = false;
    var finishIntro = function () {
      if (finished) return;
      finished = true;
      try { localStorage.setItem("dl-intro-seen", "1"); } catch (e) { }
      root.classList.add("intro-out");
      window.setTimeout(function () { root.classList.remove("intro-on", "intro-out"); }, 900);
      document.removeEventListener("keydown", onIntroKey);
    };
    var onIntroKey = function (event) { if (event.key === "Escape" || event.key === "Enter" || event.key === " ") finishIntro(); };
    document.addEventListener("keydown", onIntroKey);
    $("intro-skip").addEventListener("click", finishIntro);
    $("intro").addEventListener("click", finishIntro);
    window.setTimeout(finishIntro, 3200);
  }

  /* ---------- Chrome ---------- */

  var topbar = $("topbar");
  if (topbar && !topbar.classList.contains("is-solid")) {
    var onScroll = function () { topbar.classList.toggle("is-stuck", window.scrollY > 8); };
    window.addEventListener("scroll", onScroll, { passive: true });
    onScroll();
  }

  var revealables = all(".reveal-on-scroll");
  if (revealables.length) {
    if ("IntersectionObserver" in window) {
      var io = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            entry.target.classList.add("is-visible");
            io.unobserve(entry.target);
          }
        });
      }, { rootMargin: "0px 0px -8% 0px" });
      revealables.forEach(function (el) { io.observe(el); });
    } else {
      revealables.forEach(function (el) { el.classList.add("is-visible"); });
    }
  }

  // Floating hero cards drift slightly with the pointer.
  var floats = all(".float-card[data-depth]");
  if (floats.length && window.matchMedia("(pointer: fine)").matches && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    var frame = 0;
    window.addEventListener("pointermove", function (event) {
      if (frame) return;
      frame = window.requestAnimationFrame(function () {
        frame = 0;
        var x = event.clientX / window.innerWidth - 0.5;
        var y = event.clientY / window.innerHeight - 0.5;
        floats.forEach(function (card) {
          var depth = +card.getAttribute("data-depth");
          card.style.translate = (-x * depth).toFixed(1) + "px " + (-y * depth).toFixed(1) + "px";
        });
      });
    }, { passive: true });
  }

  all("[data-glow]").forEach(function (tile) {
    tile.addEventListener("pointermove", function (event) {
      var rect = tile.getBoundingClientRect();
      tile.style.setProperty("--mx", (event.clientX - rect.left) + "px");
      tile.style.setProperty("--my", (event.clientY - rect.top) + "px");
    });
  });

  var langToggle = $("lang-switch-toggle");
  var langMenu = $("lang-menu");
  if (langToggle && langMenu) {
    var setMenu = function (open) {
      langMenu.hidden = !open;
      langToggle.setAttribute("aria-expanded", open ? "true" : "false");
    };
    langToggle.addEventListener("click", function (event) {
      event.stopPropagation();
      setMenu(langMenu.hidden);
    });
    document.addEventListener("click", function (event) {
      if (!event.target.closest("#lang-switch")) setMenu(false);
    });
    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && !langMenu.hidden) {
        setMenu(false);
        langToggle.focus();
      }
    });
  }

  document.addEventListener("click", function (event) {
    var trigger = event.target.closest("[data-unavailable-message]");
    if (!trigger) return;
    showStatus($(trigger.getAttribute("data-status-target")), trigger.getAttribute("data-unavailable-message"));
  });

  all("[data-flow]").forEach(function (control) {
    control.addEventListener("click", function () {
      var url = control.getAttribute("data-flow");
      if (url) window.location.href = url;
    });
  });

  // Show a spinner on submit buttons so a slow upload doesn't look frozen.
  all("form").forEach(function (form) {
    form.addEventListener("submit", function (event) {
      window.setTimeout(function () {
        if (event.defaultPrevented) return;
        var submitter = event.submitter;
        if (submitter && submitter.classList.contains("btn") && submitter.hasAttribute("data-busy")) {
          submitter.classList.add("is-loading");
        }
      }, 0);
    });
  });

  /* ---------- Long documents: build the table of contents and track the active section ---------- */

  var toc = document.querySelector("[data-toc]");
  if (toc) {
    var list = toc.querySelector("ol");
    var sections = all(".legal-section > h2");
    sections.forEach(function (heading, index) {
      var section = heading.parentElement;
      if (!section.id) section.id = "s" + (index + 1);
      var item = document.createElement("li");
      var link = document.createElement("a");
      link.href = "#" + section.id;
      link.textContent = heading.textContent.replace(/^\d+\.\s*/, "");
      item.appendChild(link);
      list.appendChild(item);
    });
    if ("IntersectionObserver" in window) {
      var links = all("a", list);
      var spy = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          links.forEach(function (l) { l.classList.toggle("is-active", l.getAttribute("href") === "#" + entry.target.id); });
        });
      }, { rootMargin: "-20% 0px -70% 0px" });
      sections.forEach(function (h) { spy.observe(h.parentElement); });
    }
  }

  // Give table cells their column name so small screens can show each row as a stacked card.
  all(".legal-table").forEach(function (table) {
    var heads = all("thead th", table).map(function (th) { return th.textContent.trim(); });
    all("tbody tr", table).forEach(function (row) {
      all("td", row).forEach(function (cell, i) { if (heads[i]) cell.setAttribute("data-label", heads[i]); });
    });
  });

  /* ---------- Help centre search ---------- */

  var helpSearch = $("help-search");
  if (helpSearch) {
    var faqs = all(".faq");
    var groups = all(".faq-group");
    var empty = $("help-empty");
    var filter = function () {
      var q = helpSearch.value.trim().toLowerCase();
      var shown = 0;
      faqs.forEach(function (faq) {
        var hit = !q || faq.textContent.toLowerCase().indexOf(q) >= 0;
        faq.hidden = !hit;
        if (hit) shown++;
        if (q && hit) faq.open = true;
        if (!q) faq.open = false;
      });
      groups.forEach(function (group) { group.hidden = !all(".faq", group).some(function (f) { return !f.hidden; }); });
      empty.hidden = shown > 0;
    };
    helpSearch.addEventListener("input", filter);
    document.addEventListener("keydown", function (event) {
      if (event.key === "/" && document.activeElement !== helpSearch && !/input|textarea|select/i.test(document.activeElement.tagName)) {
        event.preventDefault();
        helpSearch.focus();
      }
    });
    if (location.hash && location.hash.length > 1) {
      var target = document.getElementById(location.hash.slice(1));
      if (target && target.tagName === "DETAILS") target.open = true;
    }
  }

  /* ---------- Password reveal ---------- */

  all("[data-reveal]").forEach(function (button) {
    var input = $(button.getAttribute("data-reveal"));
    if (!input) return;
    button.addEventListener("click", function () {
      var showing = input.type === "text";
      input.type = showing ? "password" : "text";
      button.setAttribute("aria-pressed", showing ? "false" : "true");
      button.setAttribute("aria-label", showing ? t("Show password") : t("Hide password"));
    });
  });

  /* ---------- Sign in ---------- */

  var signIn = $("sign-in-form");
  if (signIn) {
    var email = $("login-email");
    var password = $("login-password");
    var status = $("sign-in-status");

    var setHint = function (input, hint, message) {
      hint.hidden = !message;
      hint.querySelector(".hint-text").textContent = message || "";
      input.setAttribute("aria-invalid", message ? "true" : "false");
    };

    var checkEmail = function (strict) {
      var value = email.value.trim();
      if (!value) { setHint(email, $("login-email-hint"), strict ? t("Enter your email address.") : ""); return false; }
      if (!emailPattern.test(value)) { setHint(email, $("login-email-hint"), strict ? t("That doesn't look like an email address.") : ""); return false; }
      setHint(email, $("login-email-hint"), "");
      return true;
    };

    var checkPassword = function (strict) {
      if (!password.value) { setHint(password, $("login-password-hint"), strict ? t("Enter your password.") : ""); return false; }
      setHint(password, $("login-password-hint"), "");
      return true;
    };

    email.addEventListener("input", function () { checkEmail(false); });
    email.addEventListener("blur", function () { if (email.value) checkEmail(true); });
    password.addEventListener("input", function () { checkPassword(false); });

    signIn.addEventListener("submit", function (event) {
      var ok1 = checkEmail(true);
      var ok2 = checkPassword(true);
      if (!ok1 || !ok2) {
        event.preventDefault();
        showStatus(status, "");
        (ok1 ? password : email).focus();
      }
    });
  }

  all("[data-google-unavailable]").forEach(function (button) {
    button.addEventListener("click", function () {
      showStatus($(button.getAttribute("data-status-target")), GOOGLE_MESSAGE);
    });
  });

  /* ---------- Sign up ---------- */

  var signUp = $("signup-form");
  if (signUp) {
    var fullName = $("full-name");
    var signupEmail = $("email-address");
    var newPassword = $("password");
    var terms = $("terms");
    var signupStatus = $("signup-status");
    var rules = {
      length: function (v) { return v.length >= 8; },
      number: function (v) { return /[0-9]/.test(v); },
      special: function (v) { return /[^A-Za-z0-9]/.test(v); }
    };

    newPassword.addEventListener("input", function () {
      all("[data-rule]").forEach(function (rule) {
        rule.classList.toggle("is-met", rules[rule.getAttribute("data-rule")](newPassword.value));
      });
    });

    var googleForm = $("google-signup-form");
    if (googleForm) {
      googleForm.addEventListener("submit", function (event) {
        if (!terms.checked) {
          event.preventDefault();
          showStatus(signupStatus, t("Agree to the Terms of Service and Privacy Policy to continue with Google."));
          terms.focus();
          return;
        }
        $("google-terms").value = "true";
      });
    }

    signUp.addEventListener("submit", function (event) {
      var reject = function (message, field) {
        event.preventDefault();
        showStatus(signupStatus, message);
        field.focus();
      };
      var pw = newPassword.value;
      if (!fullName.value.trim()) return reject(t("Enter your full name."), fullName);
      if (!signupEmail.value.trim()) return reject(t("Enter your email address."), signupEmail);
      if (!emailPattern.test(signupEmail.value.trim())) return reject(t("Enter a valid email address."), signupEmail);
      if (!(rules.length(pw) && rules.number(pw) && rules.special(pw))) return reject(t("Use at least 8 characters, one number and one special character."), newPassword);
      if (!terms.checked) return reject(t("Agree to the Terms of Service and Privacy Policy to continue."), terms);
    });
  }

  /* ---------- Radio-style groups (gender, document type) ---------- */

  all("[data-radio-group]").forEach(function (group) {
    var input = $(group.getAttribute("data-radio-group"));
    var items = all("[role='radio']", group);

    var select = function (item, focus) {
      items.forEach(function (other) {
        var on = other === item;
        other.setAttribute("aria-checked", on ? "true" : "false");
        other.tabIndex = on ? 0 : -1;
      });
      if (input) input.value = item.getAttribute("data-value");
      if (focus) item.focus();
    };

    items.forEach(function (item, index) {
      item.addEventListener("click", function () { select(item, false); });
      item.addEventListener("keydown", function (event) {
        var delta = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }[event.key];
        if (delta) {
          event.preventDefault();
          select(items[(index + delta + items.length) % items.length], true);
        } else if (event.key === " " || event.key === "Enter") {
          event.preventDefault();
          select(item, false);
        }
      });
    });

    var current = input && input.value;
    var preset = items.filter(function (item) { return item.getAttribute("data-value") === current; })[0];
    if (preset) {
      select(preset, false);
    } else if (items.length) {
      items.forEach(function (item) { item.setAttribute("aria-checked", "false"); item.tabIndex = -1; });
      items[0].tabIndex = 0;
    }
  });

  /* ---------- City autocomplete ---------- */

  var homeCity = $("homeCity");
  var suggestions = $("city-suggestions");
  if (homeCity && suggestions) {
    var timer = null;
    var active = -1;

    var close = function () {
      suggestions.hidden = true;
      suggestions.innerHTML = "";
      homeCity.setAttribute("aria-expanded", "false");
      active = -1;
    };

    var highlight = function (index) {
      var options = all("li", suggestions);
      active = index;
      options.forEach(function (option, i) { option.setAttribute("aria-selected", i === index ? "true" : "false"); });
    };

    homeCity.addEventListener("input", function () {
      var query = homeCity.value.trim();
      window.clearTimeout(timer);
      if (query.length < 2) { close(); return; }
      timer = window.setTimeout(function () {
        fetch("/Home/CitySearch?q=" + encodeURIComponent(query), { headers: { Accept: "application/json" } })
          .then(function (response) { return response.json(); })
          .then(function (items) {
            suggestions.innerHTML = "";
            if (!items || !items.length) { close(); return; }
            items.forEach(function (item) {
              var option = document.createElement("li");
              option.setAttribute("role", "option");
              option.textContent = item.label || item.value;
              option.addEventListener("mousedown", function (event) {
                event.preventDefault();
                homeCity.value = item.value || item.label;
                close();
              });
              suggestions.appendChild(option);
            });
            suggestions.hidden = false;
            homeCity.setAttribute("aria-expanded", "true");
          })
          .catch(close);
      }, 200);
    });

    homeCity.addEventListener("keydown", function (event) {
      var options = all("li", suggestions);
      if (suggestions.hidden || !options.length) return;
      if (event.key === "ArrowDown") { event.preventDefault(); highlight((active + 1) % options.length); }
      else if (event.key === "ArrowUp") { event.preventDefault(); highlight((active - 1 + options.length) % options.length); }
      else if (event.key === "Enter" && active >= 0) {
        event.preventDefault();
        options[active].dispatchEvent(new MouseEvent("mousedown"));
      } else if (event.key === "Escape") { close(); }
    });

    homeCity.addEventListener("blur", function () { window.setTimeout(close, 120); });
  }

  /* ---------- Profile ---------- */

  var bioInput = $("bioInput");
  var previewBio = $("previewBio");
  var charCount = $("charCount");
  if (bioInput && charCount) {
    var syncBio = function () {
      var text = bioInput.value;
      charCount.textContent = text.length + " / 300";
      if (previewBio) {
        var empty = !text.trim();
        previewBio.textContent = empty ? t("Your bio will appear here.") : text;
        previewBio.classList.toggle("is-empty", empty);
      }
    };
    bioInput.addEventListener("input", syncBio);
  }

  var previewLangs = $("previewLangChips");
  var langContainer = $("languageContainer");

  var renderPreviewLanguages = function () {
    if (!previewLangs) return;
    previewLangs.textContent = "";
    var chosen = all(".lang-pill[aria-pressed='true']").map(function (pill) {
      var label = pill.querySelector("span");
      return label ? label.textContent.trim() : pill.getAttribute("data-value");
    });
    if (!chosen.length) {
      var empty = document.createElement("span");
      empty.className = "empty";
      empty.textContent = t("No languages selected");
      previewLangs.appendChild(empty);
      return;
    }
    chosen.forEach(function (language) {
      var tag = document.createElement("span");
      tag.className = "tag";
      tag.textContent = language;
      previewLangs.appendChild(tag);
    });
  };

  var bindToggle = function (button, onChange) {
    button.addEventListener("click", function () {
      var on = button.getAttribute("aria-pressed") !== "true";
      button.setAttribute("aria-pressed", on ? "true" : "false");
      button.classList.toggle("is-selected", on);
      if (onChange) onChange();
    });
  };

  all(".lang-pill").forEach(function (pill) { bindToggle(pill, renderPreviewLanguages); });
  all(".pref-card").forEach(function (card) { bindToggle(card); });

  var addLang = $("addLangBtn");
  var customRow = $("custom-lang-row");
  var customInput = $("customLangInput");
  var customSave = $("customLangSave");
  if (addLang && customRow && customInput && customSave && langContainer) {
    addLang.addEventListener("click", function () {
      customRow.hidden = !customRow.hidden;
      if (!customRow.hidden) customInput.focus();
    });

    var commit = function () {
      var language = customInput.value.trim();
      if (!language) { customInput.focus(); return; }
      var exists = all(".lang-pill").filter(function (pill) {
        return pill.getAttribute("data-value").toLowerCase() === language.toLowerCase();
      })[0];
      if (exists) {
        if (exists.getAttribute("aria-pressed") !== "true") exists.click();
      } else {
        var template = langContainer.querySelector(".lang-pill");
        var pill = template ? template.cloneNode(true) : document.createElement("button");
        pill.type = "button";
        pill.className = "chip lang-pill";
        pill.setAttribute("data-value", language);
        pill.setAttribute("aria-pressed", "true");
        pill.querySelector("span").textContent = language;
        bindToggle(pill, renderPreviewLanguages);
        langContainer.insertBefore(pill, addLang);
      }
      customInput.value = "";
      customRow.hidden = true;
      renderPreviewLanguages();
    };

    customSave.addEventListener("click", commit);
    customInput.addEventListener("keydown", function (event) {
      if (event.key === "Enter") { event.preventDefault(); commit(); }
      if (event.key === "Escape") { customRow.hidden = true; addLang.focus(); }
    });
  }

  var photoInput = $("photoUploadInput");
  var cameraInput = $("photoCameraInput");
  var previewImages = all("[data-avatar-preview]");

  var showPhoto = function (file) {
    if (!file) return;
    var reader = new FileReader();
    reader.onload = function () {
      previewImages.forEach(function (img) {
        img.src = reader.result;
        img.hidden = false;
      });
    };
    reader.readAsDataURL(file);
  };

  all("[data-photo-pick]").forEach(function (control) {
    control.addEventListener("click", function () {
      var input = $(control.getAttribute("data-photo-pick"));
      if (input) input.click();
    });
  });

  if (photoInput) {
    photoInput.addEventListener("change", function () { showPhoto(photoInput.files && photoInput.files[0]); });
  }

  if (cameraInput && photoInput) {
    cameraInput.addEventListener("change", function () {
      var file = cameraInput.files && cameraInput.files[0];
      if (!file) return;
      var transfer = new DataTransfer();
      transfer.items.add(file);
      photoInput.files = transfer.files;
      showPhoto(file);
    });
  }

  var profileForm = $("profile-form");
  if (profileForm) {
    profileForm.addEventListener("submit", function (event) {
      if (event.submitter && event.submitter.name === "removePhoto") return;
      $("languagesField").value = all(".lang-pill[aria-pressed='true']").map(function (p) { return p.getAttribute("data-value"); }).join(", ");
      $("preferencesField").value = all(".pref-card[aria-pressed='true']").map(function (p) { return p.getAttribute("data-value"); }).join(", ");
      if (bioInput && !bioInput.value.trim()) {
        event.preventDefault();
        showStatus($("flow-status"), t("Add a short bio before continuing."));
        bioInput.focus();
      }
    });
  }

  /* ---------- Contact ---------- */

  var otp = $("otp-code");
  if (otp) {
    otp.addEventListener("input", function () {
      otp.value = otp.value.replace(/\D/g, "").slice(0, 6);
    });
  }

  /* ---------- Identity ---------- */

  var documentFile = $("documentFile");
  var drop = $("doc-drop");
  if (documentFile && drop) {
    var preview = $("doc-preview");
    var img = $("doc-preview-img");
    var pdf = $("doc-preview-pdf");
    var nameEl = $("doc-preview-name");
    var stateEl = $("doc-preview-state");
    var openLink = $("doc-preview-open");
    var clearBtn = $("doc-preview-clear");
    var saved = {
      shown: preview && !preview.hidden,
      name: nameEl ? nameEl.textContent : "",
      img: img && !img.hidden ? img.getAttribute("src") : null,
      pdf: pdf && !pdf.hidden ? pdf.getAttribute("src") : null
    };
    var objectUrl = null;

    var show = function (kind, url) {
      if (img) { img.hidden = kind !== "img"; if (kind === "img") img.src = url; else img.removeAttribute("src"); }
      if (pdf) { pdf.hidden = kind !== "pdf"; if (kind === "pdf") pdf.src = url; else pdf.removeAttribute("src"); }
    };

    var reflect = function () {
      var file = documentFile.files && documentFile.files[0];
      if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
      drop.classList.toggle("has-file", !!file);

      if (file) {
        objectUrl = URL.createObjectURL(file);
        var isPdf = file.type === "application/pdf" || /\.pdf$/i.test(file.name);
        show(isPdf ? "pdf" : "img", objectUrl);
        nameEl.textContent = file.name + " · " + Math.max(1, Math.round(file.size / 1024)) + " KB";
        stateEl.textContent = preview.getAttribute("data-label-new");
        stateEl.className = "tag tag-accent";
        if (openLink) openLink.hidden = true;
        clearBtn.hidden = false;
        preview.hidden = false;
        return;
      }

      clearBtn.hidden = true;
      if (openLink) openLink.hidden = false;
      stateEl.textContent = preview.getAttribute("data-label-saved");
      stateEl.className = "tag";
      nameEl.textContent = saved.name;
      if (saved.img) show("img", saved.img);
      else if (saved.pdf) show("pdf", saved.pdf);
      else show("none");
      preview.hidden = !saved.shown;
    };

    documentFile.addEventListener("change", reflect);
    clearBtn.addEventListener("click", function () {
      documentFile.value = "";
      reflect();
    });

    ["dragenter", "dragover"].forEach(function (type) {
      drop.addEventListener(type, function (event) { event.preventDefault(); drop.classList.add("is-over"); });
    });
    ["dragleave", "drop"].forEach(function (type) {
      drop.addEventListener(type, function () { drop.classList.remove("is-over"); });
    });
    drop.addEventListener("drop", function (event) {
      event.preventDefault();
      if (event.dataTransfer && event.dataTransfer.files.length) {
        documentFile.files = event.dataTransfer.files;
        reflect();
      }
    });
  }
})();
