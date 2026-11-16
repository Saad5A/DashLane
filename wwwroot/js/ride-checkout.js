(() => {
    const get = id => document.getElementById(id);
    const page = get('ride-checkout');
    if (!page) return;
    const form = get('checkout-form');
    const promo = get('promo-code');
    const apply = get('promo-apply');
    const remove = get('promo-remove');
    const feedback = get('promo-feedback');
    const paymentFeedback = get('payment-feedback');
    const pay = get('checkout-pay');
    const canPay = !pay.disabled;
    const total = get('checkout-total');
    const discount = get('checkout-discount');
    const tax = get('checkout-tax');
    const initial = { total: Number(total.dataset.value), tax: tax.textContent };
    const money = new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' });
    let code = null;
    let amount = initial.total;
    let busy = false;

    function setBusy(value) {
        busy = value;
        apply.disabled = value || Boolean(code);
        promo.readOnly = value || Boolean(code);
        remove.disabled = value;
        pay.disabled = value || !canPay;
    }

    function showPromo(message, success) {
        feedback.textContent = message;
        feedback.toggleAttribute('data-success', Boolean(message && success));
        feedback.toggleAttribute('data-error', Boolean(message && !success));
    }

    async function post(url, payload) {
        const response = await fetch(url, {
            method: 'POST',
            redirect: 'error',
            headers: {
                'Content-Type': 'application/json',
                RequestVerificationToken: form.elements.__RequestVerificationToken.value
            },
            body: JSON.stringify({ draftId: page.dataset.draft, ...payload })
        });
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data.message || 'The request could not be completed.');
        return data;
    }

    form.addEventListener('change', () => {
        const wallet = form.elements.method.value === 'Wallet';
        get('test-card').hidden = wallet;
        get('test-wallet').hidden = !wallet;
    });

    apply.addEventListener('click', async () => {
        if (busy || code) return;
        if (!promo.value.trim()) {
            showPromo('Enter a promotion code.', false);
            return;
        }
        setBusy(true);
        try {
            const result = await post(page.dataset.promoUrl, { code: promo.value.trim() });
            code = result.code;
            promo.value = code;
            amount = result.fare.total;
            discount.textContent = `−${money.format(result.fare.discount)}`;
            tax.textContent = money.format(result.fare.tax);
            total.textContent = money.format(amount);
            remove.hidden = false;
            showPromo('Promotion applied.', true);
        } catch (error) {
            showPromo(error instanceof TypeError ? 'Sign in to apply a promotion, or try again.' : error.message, false);
        } finally {
            setBusy(false);
        }
    });

    remove.addEventListener('click', () => {
        code = null;
        amount = initial.total;
        promo.value = '';
        discount.textContent = money.format(0);
        tax.textContent = initial.tax;
        total.textContent = money.format(amount);
        remove.hidden = true;
        showPromo('', false);
        setBusy(false);
        promo.focus();
    });

    promo.addEventListener('keydown', event => {
        if (event.key !== 'Enter') return;
        event.preventDefault();
        apply.click();
    });

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (busy || !canPay) return;
        if (!code && promo.value.trim()) {
            showPromo('Apply or clear the code before continuing.', false);
            promo.focus();
            return;
        }
        setBusy(true);
        paymentFeedback.textContent = '';
        try {
            const result = await post(form.action, { method: form.elements.method.value, promoCode: code, expectedTotal: amount });
            window.location.assign(result.redirectUrl);
        } catch (error) {
            paymentFeedback.textContent = error instanceof TypeError ? 'Checkout is unavailable. Sign in or try again.' : error.message;
            setBusy(false);
        }
    });
})();
