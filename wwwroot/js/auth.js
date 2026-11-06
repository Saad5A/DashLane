(function () {
  "use strict";

  var AUTH_MESSAGE = "Google sign-in is not configured.";

  function showStatus(target, message) {
    if (!target) {
      return;
    }

    target.textContent = message;
    target.hidden = false;
  }

  document.querySelectorAll("[data-unavailable-message]").forEach(function (button) {
    button.addEventListener("click", function () {
      var target = document.getElementById(button.getAttribute("data-status-target"));
      showStatus(target, button.getAttribute("data-unavailable-message"));
    });
  });

  var form = document.getElementById("sign-in-form");
  if (!form) {
    return;
  }

  var email = document.getElementById("login-email");
  var password = document.getElementById("login-password");
  var emailHint = document.getElementById("login-email-hint");
  var passwordHint = document.getElementById("login-password-hint");
  var status = document.getElementById("sign-in-status");
  var toggle = document.getElementById("toggle-password-btn");
  var visibilityIcon = document.getElementById("password-visibility-icon");
  var google = document.getElementById("google-sign-in");
  var emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

  function setHint(hint, input, message, state) {
    var icon = hint.querySelector(".hint-icon");
    var text = hint.querySelector(".hint-text");

    hint.classList.toggle("hint-success", state === "success");
    hint.classList.toggle("hint-error", state === "error");

    if (!message) {
      hint.hidden = true;
      text.textContent = "";
      input.setAttribute("aria-invalid", "false");
      return;
    }

    hint.hidden = false;
    text.textContent = message;
    if (icon) {
      icon.textContent = state === "success" ? "check_circle" : "error";
    }
    input.setAttribute("aria-invalid", state === "error" ? "true" : "false");
  }

  function validateEmail(reportEmpty) {
    var value = email.value.trim();

    if (!value) {
      if (reportEmpty) {
        setHint(emailHint, email, "Enter your email address.", "error");
      } else {
        setHint(emailHint, email, "", "");
      }
      return false;
    }

    if (!emailPattern.test(value)) {
      setHint(emailHint, email, "Enter a valid email address.", "error");
      return false;
    }

    setHint(emailHint, email, "", "");
    return true;
  }

  function validatePassword(reportEmpty) {
    if (!password.value) {
      if (reportEmpty) {
        setHint(passwordHint, password, "Enter your password.", "error");
      } else {
        setHint(passwordHint, password, "", "");
      }
      return false;
    }

    setHint(passwordHint, password, "", "");
    return true;
  }

  email.addEventListener("input", function () {
    validateEmail(false);
  });

  email.addEventListener("blur", function () {
    validateEmail(true);
  });

  password.addEventListener("input", function () {
    if (password.value) {
      validatePassword(false);
    }
  });

  password.addEventListener("blur", function () {
    validatePassword(true);
  });

  form.addEventListener("submit", function (event) {
    var emailOk = validateEmail(true);
    var passwordOk = validatePassword(true);

    if (!emailOk || !passwordOk) {
      event.preventDefault();
      status.hidden = true;
      status.textContent = "";
      (emailOk ? password : email).focus();
    }
  });

  if (google) {
    google.addEventListener("click", function () {
      showStatus(status, AUTH_MESSAGE);
    });
  }

  if (toggle && visibilityIcon) {
    toggle.addEventListener("click", function () {
      var revealed = password.getAttribute("type") === "text";
      password.setAttribute("type", revealed ? "password" : "text");
      visibilityIcon.textContent = revealed ? "visibility" : "visibility_off";
      toggle.setAttribute("aria-label", revealed ? "Show password" : "Hide password");
      toggle.setAttribute("aria-pressed", revealed ? "false" : "true");
    });
  }
})();
