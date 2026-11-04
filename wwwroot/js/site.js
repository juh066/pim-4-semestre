class Avisos {
    constructor() { this.container = document.getElementById('notificationContainer'); }
    show(message, type = 'info', duration = 4000) {
        if (!this.container) return;
        const banner = document.createElement('div');
        banner.className = `notification-banner ${type}`;
        banner.setAttribute('role', type === 'error' ? 'alert' : 'status');
        const content = document.createElement('div');
        content.className = 'notification-content';
        content.textContent = String(message || '');
        const close = document.createElement('button');
        close.type = 'button'; close.className = 'notification-close'; close.textContent = '×'; close.setAttribute('aria-label', 'Fechar aviso');
        close.addEventListener('click', () => banner.remove());
        banner.append(content, close); this.container.appendChild(banner);
        if (duration) setTimeout(() => banner.remove(), duration);
    }
    success(message) { this.show(message, 'success'); }
    error(message) { this.show(message, 'error', 6000); }
    warning(message) { this.show(message, 'warning', 6000); }
    info(message) { this.show(message, 'info'); }
}

const avisos = new Avisos();
let csrfToken = null;

function requestId() {
    return globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

async function getCsrfToken(force = false) {
    if (csrfToken && !force) return csrfToken;
    const response = await fetch('/auth/csrf', { credentials: 'same-origin' });
    if (!response.ok) throw new Error('Não foi possível preparar a solicitação segura.');
    csrfToken = (await response.json()).token;
    return csrfToken;
}

async function apiFetch(url, options = {}) {
    const method = String(options.method || 'GET').toUpperCase();
    const headers = new Headers(options.headers || {});
    if (!['GET', 'HEAD', 'OPTIONS', 'TRACE'].includes(method)) headers.set('X-CSRF-TOKEN', await getCsrfToken());
    return fetch(url, { ...options, method, headers, credentials: 'same-origin' });
}

async function postJson(url, body = {}) {
    return apiFetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
}

async function readApiMessage(response, fallback) {
    try {
        const data = await response.json();
        if (data.errors) return Object.values(data.errors).flat().join(' ');
        return data.message || fallback;
    } catch { return fallback; }
}

function formatCurrency(value) { return Number(value || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }); }
function formatDate(value) {
    const parts = String(value || '').split('-').map(Number);
    return parts.length === 3 ? new Date(parts[0], parts[1] - 1, parts[2]).toLocaleDateString('pt-BR') : '';
}

const registrationEmailMessage = 'Informe um e-mail válido, como nome@dominio.com.';

function isValidRegistrationEmail(value) {
    const email = String(value || '').trim().toLowerCase();
    if (!email || email.length > 256 || /\s/.test(email)) return false;
    const parts = email.split('@');
    if (parts.length !== 2) return false;
    const [local, domain] = parts;
    if (!local || local.length > 64 || local.startsWith('.') || local.endsWith('.') || local.includes('..')) return false;
    if (!domain || domain.length > 253 || domain.startsWith('.') || domain.endsWith('.') || domain.includes('..')) return false;
    const labels = domain.split('.');
    if (labels.length < 2 || labels.some(label => !label || label.length > 63 || label.startsWith('-') || label.endsWith('-') || !/^[a-z0-9-]+$/i.test(label))) return false;
    const ending = labels.at(-1);
    return ending.length >= 2 && !/^\d+$/.test(ending);
}

function validateRegistrationEmail(input) {
    const help = document.getElementById('registerEmailMessage');
    const valid = isValidRegistrationEmail(input.value);
    input.setCustomValidity(valid ? '' : registrationEmailMessage);
    input.setAttribute('aria-invalid', String(!valid));
    help?.classList.toggle('is-error', !valid);
    return valid;
}

class SessionPicker {
    constructor(root, sessions) {
        this.root = root;
        this.prefix = root.dataset.sessionPicker;
        this.sessions = sessions;
        this.exposure = root.querySelector('[data-session-exposure]');
        this.date = root.querySelector('[data-session-date]');
        this.timeField = root.querySelector('[data-session-time-field]');
        this.times = root.querySelector('[data-session-times]');
        this.sessionId = root.querySelector('[data-session-id]');
        this.unavailable = root.querySelector('[data-session-unavailable]');
        this.summary = root.querySelector('[data-session-summary]');
        this.form = root.closest('form');
        this.submit = this.form?.querySelector('[data-session-submit]');
        this.quantity = this.form?.querySelector('#ticketQuantity');

        this.exposure?.addEventListener('change', () => this.selectExposure());
        this.date?.addEventListener('change', () => this.selectDate());
        this.quantity?.addEventListener('input', () => this.updateSubmit());
        this.updateSubmit();
    }

    availableSessions() {
        return this.sessions.filter(session => Number(session.available) > 0);
    }

    selectExposure() {
        this.resetDate();
        const exposureId = Number(this.exposure.value);
        if (!exposureId) return;

        const sessions = this.availableSessions().filter(session => Number(session.exposureId) === exposureId);
        const dates = [...new Set(sessions.map(session => session.date))];
        if (dates.length === 0) {
            this.showUnavailable('Não há sessões disponíveis para esta exposição.');
            return;
        }

        dates.forEach(date => this.date.add(new Option(date, date)));
        this.date.disabled = false;
    }

    selectDate() {
        this.clearTime();
        const exposureId = Number(this.exposure.value);
        if (!exposureId || !this.date.value) return;

        const sessions = this.availableSessions().filter(session =>
            Number(session.exposureId) === exposureId && session.date === this.date.value);
        if (sessions.length === 0) {
            this.showUnavailable('Não há horários disponíveis nesta data.');
            return;
        }

        sessions.forEach(session => this.addTimeOption(session));
        this.timeField.hidden = false;
        this.timeField.disabled = false;
    }

    addTimeOption(session) {
        const wrapper = document.createElement('span');
        wrapper.className = 'session-time-choice';
        const input = document.createElement('input');
        input.type = 'radio';
        input.name = `${this.prefix}SessionTime`;
        input.id = `${this.prefix}Session${session.id}`;
        input.value = session.id;
        input.addEventListener('change', () => this.selectSession(session));
        const label = document.createElement('label');
        label.htmlFor = input.id;
        label.className = 'session-time-option';
        const time = document.createElement('span');
        time.textContent = session.time;
        const availability = document.createElement('small');
        availability.textContent = `${session.available} ${Number(session.available) === 1 ? 'vaga' : 'vagas'}`;
        label.append(time, availability);
        wrapper.append(input, label);
        this.times.appendChild(wrapper);
    }

    selectSession(session) {
        this.sessionId.value = session.id;
        this.sessionId.dataset.available = session.available;
        this.summary.textContent = `${session.exposureName} · ${session.date} · ${session.time}`;
        this.summary.hidden = false;
        if (this.quantity) this.quantity.max = Math.min(20, Number(session.available));
        this.updateSubmit();
    }

    resetDate() {
        this.date.replaceChildren(new Option('Selecione uma data', ''));
        this.date.disabled = true;
        this.clearTime();
    }

    clearTime() {
        this.times.replaceChildren();
        this.timeField.hidden = true;
        this.timeField.disabled = true;
        this.sessionId.value = '';
        delete this.sessionId.dataset.available;
        this.summary.textContent = '';
        this.summary.hidden = true;
        this.unavailable.hidden = true;
        if (this.quantity) this.quantity.max = 20;
        this.updateSubmit();
    }

    showUnavailable(message) {
        this.unavailable.textContent = message;
        this.unavailable.hidden = false;
        this.updateSubmit();
    }

    updateSubmit() {
        const available = Number(this.sessionId.dataset.available || 0);
        const quantity = this.quantity ? Number(this.quantity.value) : 1;
        const validQuantity = Number.isInteger(quantity) && quantity >= 1 && quantity <= 20 && quantity <= available;
        if (this.quantity) {
            this.quantity.setCustomValidity(available > 0 && quantity > available ? `Há somente ${available} vagas disponíveis.` : '');
        }
        if (this.submit) this.submit.disabled = !this.sessionId.value || !validQuantity;
    }
}

class PerfilUsuario {
    constructor() {
        this.button = document.getElementById('userProfileIcon'); this.name = document.getElementById('userName');
        this.initials = document.getElementById('userInitials'); this.icon = document.getElementById('userIconSvg');
        this.logged = document.getElementById('loggedInActions'); this.authGrid = document.querySelector('.auth-modal-grid');
        document.getElementById('logoutBtn')?.addEventListener('click', () => this.logout());
        document.getElementById('authModal')?.addEventListener('shown.bs.modal', () => { if (this.button?.classList.contains('is-logged-in')) window.registrosUsuario?.load(); });
        this.load();
    }
    async load() {
        try {
            const response = await fetch('/auth/me', { credentials: 'same-origin' });
            if (!response.ok) return this.clear();
            this.display(await response.json());
        } catch { this.clear(); }
    }
    display(user) {
        const name = user.name || '';
        this.button?.classList.add('is-logged-in');
        if (this.button) { this.button.setAttribute('aria-label', `Usuário conectado: ${name}`); this.button.title = name; }
        if (this.name) this.name.textContent = name;
        if (this.initials) this.initials.textContent = name.split(/\s+/).slice(0, 2).map(part => part[0]?.toUpperCase()).join('');
        if (this.icon) this.icon.style.display = 'none';
        if (this.logged) this.logged.hidden = false;
        if (this.authGrid) this.authGrid.hidden = true;
        const isAdmin = user.role === 'Administrator';
        if (document.getElementById('analyticsButton')) document.getElementById('analyticsButton').hidden = !isAdmin;
        if (document.getElementById('adminButton')) document.getElementById('adminButton').hidden = !isAdmin;
        window.carrinho?.load(); window.registrosUsuario?.load();
    }
    async logout() {
        const response = await postJson('/auth/logout');
        if (response.ok) { csrfToken = null; this.clear(); avisos.info('Você foi desconectado.'); }
    }
    clear() {
        this.button?.classList.remove('is-logged-in');
        if (this.name) this.name.textContent = ''; if (this.initials) this.initials.textContent = ''; if (this.icon) this.icon.style.display = '';
        if (this.logged) this.logged.hidden = true; if (this.authGrid) this.authGrid.hidden = false;
        ['analyticsButton', 'adminButton'].forEach(id => { const element = document.getElementById(id); if (element) element.hidden = true; });
        window.carrinho?.clear(); window.registrosUsuario?.clear();
    }
}

class RegistrosUsuario {
    constructor() {
        this.tickets = document.getElementById('userTicketsList'); this.ticketsEmpty = document.getElementById('userTicketsEmpty');
        this.appointments = document.getElementById('userAppointmentsList'); this.appointmentsEmpty = document.getElementById('userAppointmentsEmpty');
        this.purchases = document.getElementById('userPurchasesList'); this.purchasesEmpty = document.getElementById('userPurchasesEmpty');
    }
    async load() { await Promise.all([this.loadReservations('tickets'), this.loadReservations('appointments'), this.loadPurchases()]); }
    async loadReservations(type) {
        const list = type === 'tickets' ? this.tickets : this.appointments;
        const empty = type === 'tickets' ? this.ticketsEmpty : this.appointmentsEmpty;
        if (!list || !empty) return;
        const response = await fetch(`/user-actions/${type}`, { credentials: 'same-origin' });
        if (!response.ok) return;
        const items = await response.json(); list.innerHTML = ''; empty.hidden = items.length > 0;
        items.forEach(item => {
            const row = document.createElement('article'); row.className = 'user-record-card';
            const content = document.createElement('div'); content.className = 'user-record-content';
            const title = document.createElement('strong'); title.textContent = item.eventName;
            const meta = document.createElement('span'); meta.textContent = `${formatDate(item.date)} às ${item.time} • Quantidade: ${item.quantity}`;
            const status = document.createElement('span'); status.className = `user-record-status ${item.status === 'cancelado' ? 'is-canceled' : item.status === 'past' ? 'is-past' : 'is-available'}`;
            status.textContent = item.status === 'cancelado' ? 'Cancelado' : item.status === 'past' ? 'Realizado' : 'Confirmado';
            content.append(title, meta); row.append(content, status);
            if (item.status === 'confirmado') {
                const cancel = document.createElement('button'); cancel.type = 'button'; cancel.className = 'user-record-cancel'; cancel.textContent = 'Cancelar';
                cancel.addEventListener('click', async () => {
                    cancel.disabled = true; const result = await apiFetch(`/user-actions/reservations/${item.id}`, { method: 'DELETE' });
                    if (result.ok) { avisos.success(await readApiMessage(result, 'Agendamento cancelado com sucesso.')); await this.loadReservations(type); }
                    else { avisos.error(await readApiMessage(result, 'Não foi possível cancelar.')); cancel.disabled = false; }
                }); row.appendChild(cancel);
            }
            list.appendChild(row);
        });
    }
    async loadPurchases() {
        if (!this.purchases || !this.purchasesEmpty) return;
        const response = await fetch('/user-actions/purchases', { credentials: 'same-origin' }); if (!response.ok) return;
        const items = await response.json(); this.purchases.innerHTML = ''; this.purchasesEmpty.hidden = items.length > 0;
        items.forEach(item => {
            const row = document.createElement('article'); row.className = 'user-record-card';
            const text = document.createElement('div'); text.className = 'user-record-content';
            const title = document.createElement('strong'); title.textContent = item.productName;
            const meta = document.createElement('span'); meta.textContent = `${item.quantity} × ${formatCurrency(item.unitPrice)} = ${formatCurrency(item.totalPrice)}`;
            const notice = document.createElement('span'); notice.textContent = item.situation;
            text.append(title, meta, notice); row.appendChild(text); this.purchases.appendChild(row);
        });
    }
    clear() { [this.tickets, this.appointments, this.purchases].forEach(item => { if (item) item.innerHTML = ''; }); [this.ticketsEmpty, this.appointmentsEmpty, this.purchasesEmpty].forEach(item => { if (item) item.hidden = false; }); }
}

class Carrinho {
    constructor() {
        this.button = document.getElementById('cartButton'); this.drawer = document.getElementById('cartDrawer'); this.backdrop = document.getElementById('cartBackdrop');
        this.closeButton = document.getElementById('cartCloseButton'); this.items = document.getElementById('cartItems'); this.empty = document.getElementById('cartEmpty');
        this.count = document.getElementById('cartCount'); this.total = document.getElementById('cartTotal'); this.checkoutButton = document.getElementById('cartCheckoutButton');
        this.button?.addEventListener('click', () => this.open()); this.closeButton?.addEventListener('click', () => this.close()); this.backdrop?.addEventListener('click', () => this.close());
        this.checkoutButton?.addEventListener('click', () => bootstrap.Modal.getOrCreateInstance(document.getElementById('paymentModal')).show());
        document.addEventListener('keydown', event => { if (event.key === 'Escape' && this.drawer?.classList.contains('is-open')) this.close(); });
        this.load();
    }
    async load() { const response = await fetch('/cart', { credentials: 'same-origin' }); if (response.ok) this.render(await response.json()); else if (response.status === 401) this.clear(); }
    async add(productId) {
        const response = await postJson('/cart/items', { productId, quantity: 1, requestId: requestId() });
        if (response.status === 401) { avisos.error('Faça login para adicionar produtos.'); bootstrap.Modal.getOrCreateInstance(document.getElementById('authModal')).show(); return; }
        if (!response.ok) return avisos.error(await readApiMessage(response, 'Não foi possível adicionar o produto.'));
        this.render(await response.json()); this.open();
    }
    async completeCheckout() {
        const response = await postJson('/cart/checkout', { requestId: requestId() });
        if (response.status === 409) {
            const data = await response.json(); if (data.cart) this.render(data.cart); avisos.warning(data.message); return false;
        }
        if (!response.ok) { avisos.error(await readApiMessage(response, 'Não foi possível concluir a compra.')); return false; }
        const data = await response.json(); this.render(data.cart); await window.registrosUsuario?.load();
        avisos.success(data.message || 'Compra registrada com sucesso. Esta operação é uma simulação para fins acadêmicos. Nenhuma cobrança foi realizada.');
        return true;
    }
    async remove(itemId) {
        const response = await apiFetch(`/cart/items/${itemId}`, { method: 'DELETE' });
        if (response.ok) this.render(await response.json()); else avisos.error(await readApiMessage(response, 'Não foi possível remover o item.'));
    }
    open() { this.previousFocus = document.activeElement; this.drawer?.classList.add('is-open'); this.drawer?.setAttribute('aria-hidden', 'false'); if (this.backdrop) { this.backdrop.hidden = false; this.backdrop.classList.add('is-open'); } this.closeButton?.focus(); this.load(); }
    close() { this.drawer?.classList.remove('is-open'); this.drawer?.setAttribute('aria-hidden', 'true'); if (this.backdrop) { this.backdrop.classList.remove('is-open'); this.backdrop.hidden = true; } this.previousFocus?.focus?.(); }
    clear() { this.render({ items: [], count: 0, total: 0 }); }
    render(cart) {
        const values = { items: Array.from(cart.items || []), count: cart.count || 0, total: cart.total || 0 };
        if (this.count) { this.count.textContent = values.count; this.count.hidden = values.count === 0; }
        if (this.total) this.total.textContent = formatCurrency(values.total); if (this.empty) this.empty.hidden = values.count > 0;
        if (this.checkoutButton) { this.checkoutButton.disabled = values.count === 0; this.checkoutButton.hidden = values.count === 0; }
        if (!this.items) return; this.items.innerHTML = '';
        values.items.forEach(item => {
            const row = document.createElement('div'); row.className = 'cart-item';
            const detail = document.createElement('div'); detail.className = 'cart-item-details';
            const name = document.createElement('strong'); name.textContent = item.productName;
            const price = document.createElement('span'); price.textContent = `${item.quantity} × ${formatCurrency(item.unitPrice)}`; detail.append(name, price);
            const action = document.createElement('div'); action.className = 'cart-item-actions';
            const subtotal = document.createElement('span'); subtotal.textContent = formatCurrency(item.subtotal);
            const remove = document.createElement('button'); remove.type = 'button'; remove.className = 'cart-item-remove'; remove.textContent = '×'; remove.setAttribute('aria-label', `Remover ${item.productName}`); remove.addEventListener('click', () => this.remove(item.id));
            action.append(subtotal, remove); row.append(detail, action); this.items.appendChild(row);
        });
    }
}

document.addEventListener('DOMContentLoaded', () => {
    window.registrosUsuario = new RegistrosUsuario(); window.carrinho = new Carrinho(); window.perfilUsuario = new PerfilUsuario();
    const catalogElement = document.getElementById('sessionCatalog');
    const sessionCatalog = catalogElement ? JSON.parse(catalogElement.textContent || '[]') : [];
    document.querySelectorAll('[data-session-picker]').forEach(root => new SessionPicker(root, sessionCatalog));

    document.getElementById('paymentForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const button = document.getElementById('paymentConfirmButton'); button.disabled = true;
        const completed = await window.carrinho.completeCheckout(); button.disabled = false;
        if (completed) { bootstrap.Modal.getOrCreateInstance(document.getElementById('paymentModal')).hide(); window.carrinho.close(); }
    });

    const registerEmail = document.getElementById('registerEmail');
    registerEmail?.addEventListener('input', () => {
        registerEmail.setCustomValidity(''); registerEmail.removeAttribute('aria-invalid');
        document.getElementById('registerEmailMessage')?.classList.remove('is-error');
    });
    registerEmail?.addEventListener('blur', () => { if (registerEmail.value) validateRegistrationEmail(registerEmail); });

    document.getElementById('registerForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        registerEmail.value = registerEmail.value.trim();
        if (!validateRegistrationEmail(registerEmail)) { registerEmail.reportValidity(); registerEmail.focus(); return; }
        const response = await postJson('/auth/register', { name: document.getElementById('registerName').value, cpf: document.getElementById('cpfInput').value, email: registerEmail.value, password: document.getElementById('registerPassword').value });
        if (!response.ok) return avisos.error(await readApiMessage(response, 'Não foi possível concluir o cadastro.'));
        avisos.success('Cadastro de visitante concluído. Faça login para continuar.'); event.target.reset();
    });

    document.getElementById('loginForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const response = await postJson('/auth/login', { email: document.getElementById('loginEmail').value, password: document.getElementById('loginPassword').value });
        if (!response.ok) return avisos.error(await readApiMessage(response, 'E-mail ou senha incorretos.'));
        csrfToken = null; window.perfilUsuario.display(await response.json()); bootstrap.Modal.getOrCreateInstance(document.getElementById('authModal')).hide(); event.target.reset(); avisos.success('Login concluído.');
    });

    document.querySelectorAll('.product').forEach(product => product.querySelector('button')?.addEventListener('click', () => window.carrinho.add(Number(product.dataset.productId))));
    document.getElementById('ticketForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const response = await postJson('/user-actions/tickets', { sessionId: Number(document.getElementById('ticketSession').value), quantity: Number(document.getElementById('ticketQuantity').value), requestId: requestId() });
        if (!response.ok) return avisos.error(await readApiMessage(response, 'Não foi possível reservar o ingresso.'));
        avisos.success(await readApiMessage(response, 'Ingresso reservado com sucesso.')); window.registrosUsuario.load();
    });
    document.getElementById('agendaForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const response = await postJson('/user-actions/appointments', { sessionId: Number(document.getElementById('agendaSession').value), requestId: requestId() });
        if (!response.ok) return avisos.error(await readApiMessage(response, 'Não foi possível agendar a visita.'));
        avisos.success(await readApiMessage(response, 'Visita agendada com sucesso.')); window.registrosUsuario.load();
    });
});
