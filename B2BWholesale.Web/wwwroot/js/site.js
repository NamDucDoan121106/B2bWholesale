// Turn any <table class="js-datatable"> into a sortable/searchable/paged DataTable.
document.addEventListener('DOMContentLoaded', function () {
    if (typeof DataTable === 'undefined') return;
    document.querySelectorAll('table.js-datatable').forEach(function (t) {
        new DataTable(t, {
            pageLength: Number(t.dataset.pageLength || 10),
            order: t.dataset.order ? JSON.parse(t.dataset.order) : [],
            columnDefs: [{ targets: 'no-sort', orderable: false, searchable: false }],
            language: { search: '', searchPlaceholder: 'Search...' }
        });
    });
});
