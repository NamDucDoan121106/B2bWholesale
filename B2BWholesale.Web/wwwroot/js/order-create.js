// Master-detail order builder: add/remove lines, live totals, client-side stock hints.
// The server re-validates everything inside a DB transaction - this script is only for UX.
(function () {
    const catalog = window.orderCatalog || [];
    const byId = new Map(catalog.map(i => [String(i.id), i]));
    const tbody = document.querySelector('#lineTable tbody');
    const template = document.getElementById('lineTemplate');
    const fmt = new Intl.NumberFormat('vi-VN');
    const money = v => fmt.format(Math.round(v)) + ' ₫';

    function buildOptions(select) {
        catalog.forEach(i => {
            const o = document.createElement('option');
            o.value = i.id;
            o.textContent = `${i.name} - ${i.size}` + (i.stock === 0 ? ' (out of stock)' : '');
            o.disabled = i.stock === 0;
            select.appendChild(o);
        });
    }

    function addLine(itemId, qty) {
        const row = template.content.firstElementChild.cloneNode(true);
        const select = row.querySelector('.item-select');
        buildOptions(select);
        if (itemId) select.value = String(itemId);
        row.querySelector('.qty-input').value = qty || 1;
        select.addEventListener('change', recalc);
        row.querySelector('.qty-input').addEventListener('input', recalc);
        row.querySelector('.remove-line').addEventListener('click', () => { row.remove(); recalc(); });
        tbody.appendChild(row);
        recalc();
        if (!itemId) select.focus();
    }

    // Renumber the inputs so MVC binds them to Lines[0], Lines[1], ...
    function renumber() {
        [...tbody.rows].forEach((row, idx) => {
            row.querySelector('.line-no').textContent = idx + 1;
            row.querySelectorAll('[data-field]').forEach(el => {
                el.name = `Lines[${idx}].${el.dataset.field}`;
                el.id = `Lines_${idx}__${el.dataset.field}`;
            });
        });
    }

    function recalc() {
        renumber();
        let units = 0, total = 0, problems = [], badQty = false;
        const requested = new Map();   // same item on several lines counts together

        [...tbody.rows].forEach(row => {
            const item = byId.get(row.querySelector('.item-select').value);
            const qtyInput = row.querySelector('.qty-input');
            const qty = Math.max(0, parseInt(qtyInput.value, 10) || 0);
            row.querySelector('.unit-price').textContent = item ? money(item.price) : '-';
            row.querySelector('.line-total').textContent = item ? money(item.price * qty) : '-';
            row.querySelector('.stock-info').textContent = item ? `In stock: ${fmt.format(item.stock)}` : '';
            if (item) {
                requested.set(item.id, (requested.get(item.id) || 0) + qty);
                units += qty;
                total += item.price * qty;
            }
            const bad = qty < 1 || qty > 10000;
            if (bad) badQty = true;
            qtyInput.classList.toggle('is-invalid', bad);
        });

        requested.forEach((qty, id) => {
            const item = byId.get(String(id));
            if (qty > item.stock) problems.push(`${item.name}: requested ${fmt.format(qty)}, only ${fmt.format(item.stock)} in stock`);
        });
        [...tbody.rows].forEach(row => {
            const item = byId.get(row.querySelector('.item-select').value);
            row.classList.toggle('table-warning', !!item && requested.get(item.id) > item.stock);
        });

        const warn = document.getElementById('stockWarn');
        warn.classList.toggle('d-none', problems.length === 0);
        warn.innerHTML = problems.length
            ? '<i class="bi bi-exclamation-triangle me-1"></i><strong>Insufficient stock</strong><br>' + problems.map(p => p.replace(/</g, '&lt;')).join('<br>')
            : '';

        const lineCount = tbody.rows.length;
        document.getElementById('emptyHint').classList.toggle('d-none', lineCount > 0);
        document.getElementById('sumLines').textContent = lineCount;
        document.getElementById('sumUnits').textContent = fmt.format(units);
        document.getElementById('sumTotal').textContent = money(total);
        document.getElementById('submitBtn').disabled = units === 0 || badQty || problems.length > 0;
    }

    document.getElementById('addLine').addEventListener('click', () => addLine());
    document.getElementById('orderForm').addEventListener('submit', e => {
        recalc();
        const btn = document.getElementById('submitBtn');
        if (btn.disabled) { e.preventDefault(); return; }
        btn.disabled = true;   // prevent double submit
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Placing order...';
    });

    const initial = window.orderInitialLines || [];
    if (initial.length) initial.forEach(l => addLine(l.itemId, l.qty));
    else addLine();
})();
