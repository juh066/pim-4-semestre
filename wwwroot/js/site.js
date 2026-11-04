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
                    if (result.ok) { avisos.success('Reserva cancelada; a vaga foi liberada.'); await this.loadReservations(type); }
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
        if (!response.ok) { avisos.error(await readApiMessage(response, 'Não foi possível confirmar a simulação.')); return false; }
        const data = await response.json(); this.render(data.cart); await window.registrosUsuario?.load(); avisos.success('Compra simulada confirmada. Nenhum pagamento foi processado.'); return true;
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

    document.getElementById('paymentForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const button = document.getElementById('paymentConfirmButton'); button.disabled = true;
        const completed = await window.carrinho.completeCheckout(); button.disabled = false;
        if (completed) { bootstrap.Modal.getOrCreateInstance(document.getElementById('paymentModal')).hide(); window.carrinho.close(); }
    });

    document.getElementById('registerForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const response = await postJson('/auth/register', { name: document.getElementById('registerName').value, cpf: document.getElementById('cpfInput').value, email: document.getElementById('registerEmail').value, password: document.getElementById('registerPassword').value });
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
        avisos.success(await readApiMessage(response, 'Ingresso reservado.')); window.registrosUsuario.load();
    });
    document.getElementById('agendaForm')?.addEventListener('submit', async event => {
        event.preventDefault(); const response = await postJson('/user-actions/appointments', { sessionId: Number(document.getElementById('agendaSession').value), requestId: requestId() });
        if (!response.ok) return avisos.error(await readApiMessage(response, 'Não foi possível agendar a visita.'));
        avisos.success(await readApiMessage(response, 'Visita agendada.')); window.registrosUsuario.load();
    });
});
