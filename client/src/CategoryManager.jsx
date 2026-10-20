import React, {
  useEffect,
  useState
} from "react";

import {
  createCategory,
  deleteCategory,
  getCategories,
  updateCategory
} from "./services/categoryService";

const emptyForm = {
  categoryCode: "",
  categoryName: "",
  description: ""
};

function CategoryManager({
  currentUser,
  onBack
}) {
  const [categories, setCategories] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [message, setMessage] = useState("");

  async function loadCategories() {
    try {
      setCategories(await getCategories());
    } catch (error) {
      setMessage(
        error.response?.data?.message ||
        "Không tải được danh mục."
      );
    }
  }

  useEffect(() => {
    loadCategories();
  }, []);

  function handleChange(event) {
    setForm({
      ...form,
      [event.target.name]: event.target.value
    });
  }

  async function handleSubmit(event) {
    event.preventDefault();

    try {
      if (editingId) {
        await updateCategory(editingId, form);
        setMessage("Cập nhật danh mục thành công.");
      } else {
        await createCategory(form);
        setMessage("Thêm danh mục thành công.");
      }

      setEditingId(null);
      setForm(emptyForm);
      await loadCategories();
    } catch (error) {
      setMessage(
        error.response?.data?.message ||
        "Không thể lưu danh mục."
      );
    }
  }

  function handleEdit(category) {
    setEditingId(category.Id);

    setForm({
      categoryCode: category.CategoryCode || "",
      categoryName: category.CategoryName || "",
      description: category.Description || ""
    });
  }

  async function handleDelete(id) {
    if (
      !window.confirm(
        "Bạn có chắc chắn muốn xóa danh mục?"
      )
    ) {
      return;
    }

    try {
      await deleteCategory(id);
      setMessage("Xóa danh mục thành công.");
      await loadCategories();
    } catch (error) {
      setMessage(
        error.response?.data?.message ||
        "Không thể xóa danh mục."
      );
    }
  }

  return (
    <main className="container">
      <header>
        <div>
          <p className="eyebrow">HEXA GROUP</p>
          <h1>Quản lý danh mục</h1>
        </div>

        <button onClick={onBack}>
          Quay lại tài sản
        </button>
      </header>

      {currentUser.role === "admin" && (
        <form
          className="card"
          onSubmit={handleSubmit}
        >
          <h2>
            {editingId
              ? "Cập nhật danh mục"
              : "Thêm danh mục"}
          </h2>

          <div className="grid">
            <label>
              Mã danh mục
              <input
                name="categoryCode"
                value={form.categoryCode}
                onChange={handleChange}
                required
              />
            </label>

            <label>
              Tên danh mục
              <input
                name="categoryName"
                value={form.categoryName}
                onChange={handleChange}
                required
              />
            </label>
          </div>

          <label>
            Mô tả
            <textarea
              name="description"
              value={form.description}
              onChange={handleChange}
              rows="3"
            />
          </label>

          <div className="actions">
            <button type="submit">
              {editingId
                ? "Lưu thay đổi"
                : "Thêm danh mục"}
            </button>

            {editingId && (
              <button
                type="button"
                className="muted"
                onClick={() => {
                  setEditingId(null);
                  setForm(emptyForm);
                }}
              >
                Hủy
              </button>
            )}
          </div>
        </form>
      )}

      {message && (
        <p className="message">{message}</p>
      )}

      <section className="card">
        <h2>Danh sách danh mục</h2>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Mã</th>
                <th>Tên danh mục</th>
                <th>Mô tả</th>
                <th>Số tài sản</th>
                {currentUser.role === "admin" && (
                  <th>Thao tác</th>
                )}
              </tr>
            </thead>

            <tbody>
              {categories.map((category) => (
                <tr key={category.Id}>
                  <td className="code">
                    {category.CategoryCode}
                  </td>

                  <td>{category.CategoryName}</td>
                  <td>{category.Description || "-"}</td>
                  <td>{category.AssetCount}</td>

                  {currentUser.role === "admin" && (
                    <td>
                      <div className="row-actions">
                        <button
                          onClick={() =>
                            handleEdit(category)
                          }
                        >
                          Sửa
                        </button>

                        <button
                          className="danger"
                          onClick={() =>
                            handleDelete(category.Id)
                          }
                        >
                          Xóa
                        </button>
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </main>
  );
}

export default CategoryManager;