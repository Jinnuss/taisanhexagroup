import api from "./api";

export async function getAssets(params = {}) {
  const response = await api.get("/assets", { params });
  return response.data;
}

export async function getAssetStats() {
  const response = await api.get("/assets/stats");
  return response.data;
}

export async function createAsset(asset) {
  const response = await api.post("/assets", asset);
  return response.data;
}

export async function updateAsset(id, asset) {
  const response = await api.put(`/assets/${id}`, asset);
  return response.data;
}

export async function deleteAsset(id) {
  const response = await api.delete(`/assets/${id}`);
  return response.data;
}

function downloadBlob(blob, filename) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export async function exportAssets(params = {}) {
  const response = await api.get("/assets/excel/export", { params, responseType: "blob" });
  downloadBlob(response.data, `tai-san-hexa-${new Date().toISOString().slice(0, 10)}.xlsx`);
}

export async function downloadImportTemplate() {
  const response = await api.get("/assets/excel/template", { responseType: "blob" });
  downloadBlob(response.data, "mau-import-tai-san.xlsx");
}

export async function importAssets(file) {
  const formData = new FormData();
  formData.append("file", file);
  const response = await api.post("/assets/excel/import", formData, {
    headers: { "Content-Type": undefined }
  });
  return response.data;
}
