import api from "./api";

export async function getAssigneeSuggestions() {
  const response = await api.get("/assets/assignee-suggestions");
  return response.data;
}

export async function getAssetHistory(assetId) {
  const response = await api.get(`/assets/${assetId}/history`);
  return response.data;
}

export async function assignAsset(assetId, data) {
  const response = await api.post(`/assets/${assetId}/assign`, data);
  return response.data;
}

export async function returnAsset(assetId, data) {
  const response = await api.post(`/assets/${assetId}/return`, data);
  return response.data;
}
