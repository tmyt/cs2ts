// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.body.addEventListener('dragenter', e => {
  e.stopPropagation();
  e.preventDefault();
});

document.body.addEventListener('dragover', e => {
  e.stopPropagation();
  e.preventDefault();
});

document.body.addEventListener('drop', e => {
  e.stopPropagation();
  e.preventDefault();
  const dt = e.dataTransfer;
  const files = dt.files;
  if (!files || !files.length) return;
  const textarea = document.getElementById("Source");
  const form = document.getElementById("form");
  Promise.all(Array.from(files).map(p => p.text()))
    .then(texts => textarea.value = texts.join("\n"))
    .then(() => form.submit());
});