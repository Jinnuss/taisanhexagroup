import api from "./api";

export async function getCategories() {
  const response = await api.get("/categories");
  return response.data;
}

export async function createCategory(data) {
  const response = await api.post(
    "/categories",
    data
  );

  return response.data;
}

export async function updateCategory(id, data) {
  const response = await api.put(
    `/categories/${id}`,
    data
  );

  return response.data;
}

export async function deleteCategory(id) {
  const response = await api.delete(
    `/categories/${id}`
  );

  return response.data;
}