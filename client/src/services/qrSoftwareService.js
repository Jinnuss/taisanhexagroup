import api from "./api";
export const getQr = async (id) => (await api.get(`/assets/${id}/qr`)).data;
export const getSoftware = async (id) => (await api.get(`/assets/${id}/software`)).data;
export const addSoftware = async (id, data) => (await api.post(`/assets/${id}/software`, { ...data, installedDate: data.installedDate || null })).data;
export const deleteSoftware = async (id, softwareId) => (await api.delete(`/assets/${id}/software/${softwareId}`)).data;
export const getPublicAsset = async (publicId) => (await api.get(`/public/assets/${publicId}`)).data;
export const getImages = async (id) => (await api.get(`/assets/${id}/images`)).data;
export const uploadImages = async (id, files) => {
  const form = new FormData();
  Array.from(files).forEach((file) => form.append("images", file));
  // Không tự gán multipart/form-data: trình duyệt phải tự thêm boundary cho FormData.
  return (await api.post(`/assets/${id}/images`, form, { headers: { "Content-Type": undefined } })).data;
};
export const setPrimaryImage = async (id, imageId) => (await api.put(`/assets/${id}/images/${imageId}/primary`)).data;
export const deleteAssetImage = async (id, imageId) => (await api.delete(`/assets/${id}/images/${imageId}`)).data;
