(function () {
  "use strict";

  var AUTH_MESSAGE = "Google sign-in is not configured.";
  var emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  document.querySelectorAll("[data-stop-label]").forEach(function (button) {
    button.addEventListener("click", function (event) {
      event.preventDefault();
      event.stopPropagation();
    });
  });

  document.addEventListener("click", function (event) {
    var button = event.target.closest("[data-unavailable-message]");
    if (!button || button.getAttribute("data-bound") === "auth") {
      return;
    }

    var target = document.getElementById(button.getAttribute("data-status-target"));
    if (!target || target.textContent === button.getAttribute("data-unavailable-message")) {
      return;
    }

    target.textContent = button.getAttribute("data-unavailable-message");
    target.hidden = false;
  });

  function showStatus(target, message) {
    if (!target) {
      return;
    }

    target.textContent = message;
    target.hidden = !message;
  }

  function go(url) {
    if (url) {
      window.location.href = url;
    }
  }

  document.querySelectorAll("[data-flow]").forEach(function (control) {
    if (control.id === "btn-complete-profile" || control.id === "submitBtn") {
      return;
    }

    control.addEventListener("click", function () {
      go(control.getAttribute("data-flow"));
    });
  });

  document.querySelectorAll("[data-photo-pick]").forEach(function (control) {
    control.addEventListener("click", function () {
      var input = document.getElementById(control.getAttribute("data-photo-pick"));
      if (input) {
        input.click();
      }
    });
  });

  var completeProfile = document.getElementById("btn-complete-profile");
  if (completeProfile) {
    completeProfile.addEventListener("click", function () {
      var original = completeProfile.innerHTML;
      completeProfile.classList.add("opacity-90");
      completeProfile.innerHTML = '<span class="material-symbols-outlined text-[20px] animate-spin">progress_activity</span><span>Preparing Step 2...</span>';
      window.setTimeout(function () {
        go(completeProfile.getAttribute("data-flow"));
        completeProfile.innerHTML = original;
        completeProfile.classList.remove("opacity-90");
      }, 800);
    });
  }

  document.querySelectorAll(".gender-pill").forEach(function (button) {
    button.addEventListener("click", function () {
      selectGender(button);
    });
  });

  var savedGender = document.getElementById("gender");
  if (savedGender && savedGender.value) {
    document.querySelectorAll(".gender-pill").forEach(function (button) {
      var label = button.querySelector("span:last-child");
      if (label && label.textContent.trim() === savedGender.value) {
        selectGender(button);
      }
    });
  }

  function selectGender(button) {
    var group = button.closest('[role="radiogroup"]');
    var buttons = group.querySelectorAll(".gender-pill");

    buttons.forEach(function (item) {
      item.setAttribute("aria-checked", "false");
      item.className = "gender-pill px-space-md py-2.5 rounded-full bg-surface-container-low text-on-surface font-label-badge text-label-badge transition-colors hover:bg-surface-container active:scale-95 flex items-center gap-1.5";
      var existing = item.querySelector(".material-symbols-outlined");
      if (existing) {
        existing.remove();
      }
    });

    button.setAttribute("aria-checked", "true");
    button.className = "gender-pill px-space-md py-2.5 rounded-full bg-primary-container text-on-primary font-label-badge text-label-badge shadow-sm transition-transform active:scale-95 flex items-center gap-1.5";
    var genderInput = document.getElementById("gender");
    if (genderInput) {
      var label = button.querySelector("span:last-child");
      genderInput.value = label ? label.textContent.trim() : button.textContent.trim();
    }
    var icon = document.createElement("span");
    icon.className = "material-symbols-outlined text-[16px]";
    icon.textContent = "check_circle";
    button.prepend(icon);
  }

  var bioInput = document.getElementById("bioInput");
  var previewBio = document.getElementById("previewBio");
  var charCount = document.getElementById("charCount");
  if (bioInput && previewBio && charCount) {
    bioInput.addEventListener("input", function () {
      var text = bioInput.value;
      charCount.textContent = text.length + " / 300 characters";
      previewBio.textContent = text.trim().length > 0 ? '"' + text + '"' : '"No bio provided yet."';
    });
  }

  var previewLangChips = document.getElementById("previewLangChips");
  document.querySelectorAll(".lang-pill").forEach(function (pill) {
    pill.addEventListener("click", function () {
      var selected = pill.classList.contains("bg-secondary");
      if (selected) {
        pill.classList.remove("bg-secondary", "text-on-secondary");
        pill.classList.add("bg-surface-container-low", "text-on-surface-variant");
        var checkIcon = pill.querySelector(".material-symbols-outlined");
        if (checkIcon) {
          checkIcon.remove();
        }
      } else {
        pill.classList.remove("bg-surface-container-low", "text-on-surface-variant");
        pill.classList.add("bg-secondary", "text-on-secondary");
        var icon = document.createElement("span");
        icon.className = "material-symbols-outlined text-[14px]";
        icon.textContent = "check";
        pill.prepend(icon);
      }
      updatePreviewLanguages();
    });
  });

  function updatePreviewLanguages() {
    if (!previewLangChips) {
      return;
    }

    previewLangChips.textContent = "";
    var activeLanguages = [];
    document.querySelectorAll(".lang-pill.bg-secondary").forEach(function (pill) {
      var textSpan = pill.querySelector("span:last-child");
      if (textSpan) {
        activeLanguages.push(textSpan.textContent.trim());
      }
    });

    if (activeLanguages.length === 0) {
      var empty = document.createElement("span");
      empty.className = "text-caption font-caption text-on-surface-variant italic";
      empty.textContent = "None selected";
      previewLangChips.appendChild(empty);
      return;
    }

    activeLanguages.forEach(function (language) {
      var chip = document.createElement("span");
      chip.className = "bg-surface-container px-space-sm py-[2px] rounded-full font-caption text-caption text-on-surface font-medium";
      chip.textContent = language;
      previewLangChips.appendChild(chip);
    });
  }

  var photoUploadInput = document.getElementById("photoUploadInput");
  var photoCameraInput = document.getElementById("photoCameraInput");
  var avatarImage = document.getElementById("avatarImage");
  var previewAvatar = document.getElementById("previewAvatar");
  function previewPhotoFile(file) {
    if (!file) {
      return;
    }

    var reader = new FileReader();
    reader.onload = function () {
      if (avatarImage) {
        avatarImage.src = reader.result;
        avatarImage.style.display = "block";
      }
      if (previewAvatar) {
        previewAvatar.src = reader.result;
        previewAvatar.style.display = "block";
      }
    };
    reader.readAsDataURL(file);
  }

  function assignPhotoFile(file) {
    if (!photoUploadInput || !file) {
      return;
    }

    var transfer = new DataTransfer();
    transfer.items.add(file);
    photoUploadInput.files = transfer.files;
    previewPhotoFile(file);
  }

  if (photoUploadInput) {
    photoUploadInput.addEventListener("change", function () {
      previewPhotoFile(photoUploadInput.files && photoUploadInput.files[0]);
    });
  }

  if (photoCameraInput) {
    photoCameraInput.addEventListener("change", function () {
      assignPhotoFile(photoCameraInput.files && photoCameraInput.files[0]);
    });
  }

  var addLangBtn = document.getElementById("addLangBtn");
  var customLangRow = document.getElementById("custom-lang-row");
  var customLangInput = document.getElementById("customLangInput");
  var customLangSave = document.getElementById("customLangSave");
  if (addLangBtn && customLangRow) {
    addLangBtn.addEventListener("click", function () {
      customLangRow.hidden = !customLangRow.hidden;
      if (!customLangRow.hidden && customLangInput) {
        customLangInput.focus();
      }
    });
  }

  if (customLangSave && customLangInput) {
    customLangSave.addEventListener("click", function () {
      var language = customLangInput.value.trim();
      if (!language) {
        customLangInput.focus();
        return;
      }

      var existing = Array.prototype.some.call(document.querySelectorAll(".lang-pill"), function (pill) {
        var textSpan = pill.querySelector("span:last-child");
        return textSpan && textSpan.textContent.trim().toLowerCase() === language.toLowerCase();
      });
      if (existing) {
        customLangInput.value = "";
        return;
      }

      var pill = document.createElement("button");
      pill.type = "button";
      pill.className = "lang-pill px-space-md py-space-xs rounded-full bg-secondary text-on-secondary font-label-badge text-label-badge flex items-center gap-space-2xs transition-all shadow-sm";
      pill.innerHTML = '<span class="material-symbols-outlined text-[14px]">check</span><span></span>';
      pill.querySelector("span:last-child").textContent = language;
      pill.addEventListener("click", function () {
        var selected = pill.classList.contains("bg-secondary");
        if (selected) {
          pill.classList.remove("bg-secondary", "text-on-secondary");
          pill.classList.add("bg-surface-container-low", "text-on-surface-variant");
          var checkIcon = pill.querySelector(".material-symbols-outlined");
          if (checkIcon) {
            checkIcon.remove();
          }
        } else {
          pill.classList.remove("bg-surface-container-low", "text-on-surface-variant");
          pill.classList.add("bg-secondary", "text-on-secondary");
          var icon = document.createElement("span");
          icon.className = "material-symbols-outlined text-[14px]";
          icon.textContent = "check";
          pill.prepend(icon);
        }
        updatePreviewLanguages();
      });
      if (addLangBtn && addLangBtn.parentNode) {
        addLangBtn.parentNode.insertBefore(pill, addLangBtn);
      }
      customLangInput.value = "";
      customLangRow.hidden = true;
      updatePreviewLanguages();
    });
  }

  var homeCity = document.getElementById("homeCity");
  var citySuggestions = document.getElementById("city-suggestions");
  var cityTimer = null;
  if (homeCity && citySuggestions) {
    homeCity.addEventListener("input", function () {
      var query = homeCity.value.trim();
      window.clearTimeout(cityTimer);
      if (query.length < 2) {
        citySuggestions.hidden = true;
        citySuggestions.innerHTML = "";
        return;
      }

      cityTimer = window.setTimeout(function () {
        fetch("/Home/CitySearch?q=" + encodeURIComponent(query), { headers: { Accept: "application/json" } })
          .then(function (response) { return response.json(); })
          .then(function (items) {
            citySuggestions.innerHTML = "";
            if (!items || !items.length) {
              citySuggestions.hidden = true;
              return;
            }

            items.forEach(function (item) {
              var option = document.createElement("li");
              option.setAttribute("role", "option");
              option.textContent = item.label || item.value;
              option.addEventListener("click", function () {
                homeCity.value = item.value || item.label;
                citySuggestions.hidden = true;
                citySuggestions.innerHTML = "";
              });
              citySuggestions.appendChild(option);
            });
            citySuggestions.hidden = false;
          })
          .catch(function () {
            citySuggestions.hidden = true;
          });
      }, 250);
    });

    document.addEventListener("click", function (event) {
      if (!event.target.closest("#city-autocomplete")) {
        citySuggestions.hidden = true;
      }
    });
  }

  var langToggle = document.getElementById("lang-switch-toggle");
  var langMenu = document.getElementById("lang-menu");
  if (langToggle && langMenu) {
    langToggle.addEventListener("click", function (event) {
      event.preventDefault();
      event.stopPropagation();
      var open = langMenu.hasAttribute("hidden");
      if (open) {
        langMenu.removeAttribute("hidden");
        langToggle.setAttribute("aria-expanded", "true");
      } else {
        langMenu.setAttribute("hidden", "");
        langToggle.setAttribute("aria-expanded", "false");
      }
    });
    document.addEventListener("click", function (event) {
      if (!event.target.closest("#lang-switch")) {
        langMenu.setAttribute("hidden", "");
        langToggle.setAttribute("aria-expanded", "false");
      }
    });
  }

  var countdownEl = document.getElementById("countdown-timer");
  if (countdownEl) {
    var seconds = 42;
    var timer = window.setInterval(function () {
      seconds -= 1;
      if (seconds >= 0) {
        countdownEl.textContent = "0:" + (seconds < 10 ? "0" : "") + seconds;
        return;
      }

      window.clearInterval(timer);
      if (!countdownEl.parentElement) {
        return;
      }

      var resend = document.createElement("button");
      resend.type = "button";
      resend.className = "text-primary font-bold hover:underline cursor-pointer";
      resend.setAttribute("data-unavailable-message", "Sending a new code is not available yet.");
      resend.setAttribute("data-status-target", "flow-status");
      resend.textContent = "Resend code now";
      countdownEl.parentElement.textContent = "";
      countdownEl.parentElement.appendChild(resend);
    }, 1000);
  }

  document.querySelectorAll(".doc-card").forEach(function (card) {
    card.addEventListener("click", function () {
      document.querySelectorAll(".doc-card").forEach(function (item) {
        item.classList.remove("bg-surface-container-high", "shadow-sm");
        item.classList.add("bg-surface-container-low");
        var icon = item.querySelector(".check-icon");
        if (icon) {
          icon.textContent = "radio_button_unchecked";
          icon.classList.remove("text-primary");
          icon.classList.add("text-outline-variant");
          icon.style.fontVariationSettings = "'FILL' 0";
        }
      });

      card.classList.remove("bg-surface-container-low");
      card.classList.add("bg-surface-container-high", "shadow-sm");
      var documentType = document.getElementById("documentType");
      if (documentType) {
        documentType.value = card.getAttribute("data-document-type") || "";
      }
      var selected = card.querySelector(".check-icon");
      if (selected) {
        selected.textContent = "check_circle";
        selected.classList.remove("text-outline-variant");
        selected.classList.add("text-primary");
        selected.style.fontVariationSettings = "'FILL' 1";
      }
    });
  });

  var submitBtn = document.getElementById("submitBtn");
  if (submitBtn) {
    submitBtn.addEventListener("click", function () {
      var original = submitBtn.innerHTML;
      submitBtn.innerHTML = '<span class="material-symbols-outlined animate-spin text-[18px]">progress_activity</span><span>Encrypting &amp; Validating...</span>';
      submitBtn.classList.add("opacity-90");
      window.setTimeout(function () {
        submitBtn.innerHTML = '<span class="material-symbols-outlined text-[18px]">check</span><span>Verified &amp; Proceeding!</span>';
        submitBtn.classList.remove("opacity-90");
        submitBtn.classList.add("bg-secondary");
        window.setTimeout(function () {
          go(submitBtn.getAttribute("data-flow"));
          submitBtn.innerHTML = original;
        }, 500);
      }, 1200);
    });
  }

  var profileForm = document.getElementById("profile-form");
  if (profileForm) {
    profileForm.addEventListener("submit", function (event) {
      var submitter = event.submitter;
      if (submitter && submitter.getAttribute("name") === "removePhoto") {
        return;
      }

      var languages = [];
      document.querySelectorAll(".lang-pill.bg-secondary").forEach(function (pill) {
        var textSpan = pill.querySelector("span:last-child");
        if (textSpan) {
          languages.push(textSpan.textContent.trim());
        }
      });
      var languagesField = document.getElementById("languagesField");
      if (languagesField) {
        languagesField.value = languages.join(", ");
      }

      var preferences = [];
      document.querySelectorAll(".pref-card.is-selected").forEach(function (card) {
        var label = card.querySelector(".font-medium");
        if (label) {
          preferences.push(label.textContent.trim());
        }
      });
      var preferencesField = document.getElementById("preferencesField");
      if (preferencesField) {
        preferencesField.value = preferences.join(", ");
      }

      if (bioInput && !bioInput.value.trim()) {
        event.preventDefault();
        showStatus(document.getElementById("flow-status"), "Add a short bio before continuing.");
        bioInput.focus();
      }
    });
  }

  function setPrefIcon(card, selected) {
    var icon = card.querySelector(".pref-check");
    if (!icon) {
      return;
    }

    icon.textContent = selected ? "check_circle" : "radio_button_unchecked";
    icon.classList.toggle("text-secondary", selected);
    icon.classList.toggle("text-outline-variant", !selected);
  }

  var savedPreferences = document.getElementById("preferencesField");
  if (savedPreferences) {
    var saved = savedPreferences.value.split(",").map(function (item) { return item.trim(); }).filter(Boolean);
    document.querySelectorAll(".pref-card").forEach(function (card) {
      var label = card.querySelector(".font-medium");
      var text = label ? label.textContent.trim() : "";
      var selected = saved.indexOf(text) >= 0;
      card.classList.toggle("is-selected", selected);
      setPrefIcon(card, selected);
    });
  }

  document.querySelectorAll(".pref-card").forEach(function (card) {
    card.addEventListener("click", function () {
      card.classList.toggle("is-selected");
      setPrefIcon(card, card.classList.contains("is-selected"));
    });
  });

  var signup = document.getElementById("signup-form");
  if (!signup) {
    return;
  }

  var fullName = document.getElementById("full-name");
  var email = document.getElementById("email-address");
  var passwordInput = document.getElementById("password");
  var terms = document.getElementById("terms");
  var signupStatus = document.getElementById("signup-status");
  var toggleBtn = document.getElementById("toggle-password");
  var eyeIcon = document.getElementById("eye-icon");
  var google = document.getElementById("google-sign-up");
  var reqLength = document.getElementById("req-length");
  var reqLengthIcon = document.getElementById("req-length-icon");
  var reqNumber = document.getElementById("req-number");
  var reqNumberIcon = document.getElementById("req-number-icon");
  var reqSpecial = document.getElementById("req-special");
  var reqSpecialIcon = document.getElementById("req-special-icon");

  function updateReqState(container, icon, isValid) {
    if (!container || !icon) {
      return;
    }

    if (isValid) {
      container.classList.remove("bg-surface-container", "text-on-surface-variant");
      container.classList.add("bg-secondary-fixed/50", "text-secondary");
      icon.textContent = "check_circle";
      icon.style.fontVariationSettings = "'FILL' 1";
    } else {
      container.classList.remove("bg-secondary-fixed/50", "text-secondary");
      container.classList.add("bg-surface-container", "text-on-surface-variant");
      icon.textContent = "radio_button_unchecked";
      icon.style.fontVariationSettings = "'FILL' 0";
    }
  }

  if (toggleBtn && passwordInput && eyeIcon) {
    toggleBtn.addEventListener("click", function () {
      var showing = passwordInput.getAttribute("type") === "text";
      passwordInput.setAttribute("type", showing ? "password" : "text");
      eyeIcon.textContent = showing ? "visibility" : "visibility_off";
      toggleBtn.setAttribute("aria-pressed", showing ? "false" : "true");
      toggleBtn.setAttribute("aria-label", showing ? "Show password" : "Hide password");
    });
  }

  if (passwordInput) {
    passwordInput.addEventListener("input", function () {
      var value = passwordInput.value;
      updateReqState(reqLength, reqLengthIcon, value.length >= 8);
      updateReqState(reqNumber, reqNumberIcon, /[0-9]/.test(value));
      updateReqState(reqSpecial, reqSpecialIcon, /[^A-Za-z0-9]/.test(value));
    });
  }

  var googleSignupForm = document.getElementById("google-signup-form");
  if (googleSignupForm) {
    googleSignupForm.addEventListener("submit", function (event) {
      if (!terms || !terms.checked) {
        event.preventDefault();
        showStatus(signupStatus, "Agree to the Terms of Service and Privacy Policy to continue with Google.");
        if (terms) {
          terms.focus();
        }
        return;
      }

      var googleTerms = document.getElementById("google-terms");
      if (googleTerms) {
        googleTerms.value = "true";
      }
    });
  } else if (google && google.getAttribute("data-google-unavailable") === "true") {
    google.addEventListener("click", function () {
      showStatus(signupStatus, AUTH_MESSAGE);
    });
  }

  signup.addEventListener("submit", function (event) {
    var nameValue = fullName.value.trim();
    var emailValue = email.value.trim();
    var passwordValue = passwordInput.value;

    function reject(message, field) {
      event.preventDefault();
      showStatus(signupStatus, message);
      field.focus();
    }

    if (!nameValue) {
      reject("Enter your full name.", fullName);
      return;
    }

    if (!emailValue) {
      reject("Enter your email address.", email);
      return;
    }

    if (!emailPattern.test(emailValue)) {
      reject("Enter a valid email address.", email);
      return;
    }

    if (!(passwordValue.length >= 8 && /[0-9]/.test(passwordValue) && /[^A-Za-z0-9]/.test(passwordValue))) {
      reject("Use at least 8 characters, one number, and one special character.", passwordInput);
      return;
    }

    if (!terms.checked) {
      reject("Agree to the Terms of Service and Privacy Policy to continue.", terms);
    }
  });
})();
