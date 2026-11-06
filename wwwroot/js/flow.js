(function () {
  "use strict";

  var AUTH_MESSAGE = "Authentication is not connected yet.";
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
  var avatarImage = document.getElementById("avatarImage");
  var previewAvatar = document.getElementById("previewAvatar");
  var removePhotoBtn = document.getElementById("removePhotoBtn");
  var originalAvatar = avatarImage ? avatarImage.getAttribute("src") : "";
  var originalPreview = previewAvatar ? previewAvatar.getAttribute("src") : "";

  if (photoUploadInput) {
    photoUploadInput.addEventListener("change", function () {
      var file = photoUploadInput.files && photoUploadInput.files[0];
      if (!file) {
        return;
      }

      var reader = new FileReader();
      reader.onload = function () {
        if (avatarImage) {
          avatarImage.src = reader.result;
        }
        if (previewAvatar) {
          previewAvatar.src = reader.result;
        }
      };
      reader.readAsDataURL(file);
    });
  }

  if (removePhotoBtn) {
    removePhotoBtn.addEventListener("click", function () {
      if (avatarImage) {
        avatarImage.src = originalAvatar;
      }
      if (previewAvatar) {
        previewAvatar.src = originalPreview;
      }
      if (photoUploadInput) {
        photoUploadInput.value = "";
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

  var finishBtn = document.getElementById("finishBtn");
  var reviewStatus = document.getElementById("review-status");
  if (finishBtn) {
    finishBtn.addEventListener("click", function () {
      var original = finishBtn.innerHTML;
      finishBtn.classList.add("opacity-90");
      finishBtn.innerHTML = '<span class="material-symbols-outlined text-[20px] animate-spin">refresh</span><span>Redirecting to Feed...</span>';
      window.setTimeout(function () {
        finishBtn.innerHTML = original;
        finishBtn.classList.remove("opacity-90");
        showStatus(reviewStatus, "The ride feed is not available yet.");
      }, 900);
    });
  }

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

  if (google) {
    google.addEventListener("click", function () {
      showStatus(signupStatus, AUTH_MESSAGE);
    });
  }

  signup.addEventListener("submit", function (event) {
    event.preventDefault();
    var nameValue = fullName.value.trim();
    var emailValue = email.value.trim();
    var passwordValue = passwordInput.value;

    if (!nameValue) {
      showStatus(signupStatus, "Enter your full name.");
      fullName.focus();
      return;
    }

    if (!emailValue) {
      showStatus(signupStatus, "Enter your email address.");
      email.focus();
      return;
    }

    if (!emailPattern.test(emailValue)) {
      showStatus(signupStatus, "Enter a valid email address.");
      email.focus();
      return;
    }

    if (!(passwordValue.length >= 8 && /[0-9]/.test(passwordValue) && /[^A-Za-z0-9]/.test(passwordValue))) {
      showStatus(signupStatus, "Use at least 8 characters, one number, and one special character.");
      passwordInput.focus();
      return;
    }

    if (!terms.checked) {
      showStatus(signupStatus, "Agree to the Terms of Service and Privacy Policy to continue.");
      terms.focus();
      return;
    }

    showStatus(signupStatus, "");
    go(signup.getAttribute("data-next"));
  });
})();
