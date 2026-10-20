import React, { useEffect, useState } from "react";
import { getPublicAsset } from "./services/qrSoftwareService";

const labels = { available: "Có sẵn", using: "Đã sử dụng", maintenance: "Bảo trì", broken: "Đã hỏng", liquidated: "Đã thanh lý" };

export default function PublicAssetPage({ publicId }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState("");
  useEffect(() => { getPublicAsset(publicId).then(setData).catch((e) => setError(e.response?.data?.message || "Không tải được tài sản.")); }, [publicId]);
  if (error) return <main className="public-page"><div className="public-card"><h1>Không tìm thấy tài sản</h1><p>{error}</p></div></main>;
  if (!data) return <main className="public-page"><div className="public-card">Đang tải thông tin...</div></main>;
  const { asset, assignments, software, images = [] } = data;
  const isComputer = ["LAPTOP", "PC", "DESKTOP"].includes(String(asset.CategoryCode || "").toUpperCase());
  return <main className="public-page">
    <div className="public-card">
      <p className="eyebrow">HEXA GROUP · THÔNG TIN TÀI SẢN</p>
      <div className="public-heading"><div><h1>{asset.AssetName}</h1><strong>{asset.AssetCode}</strong></div><span className={`status ${asset.Status}`}>● {labels[asset.Status] || asset.Status}</span></div>
      {images.length > 0 && <div className="public-image-gallery">
        {images.map((image) => <img key={image.Id} className={image.IsPrimary ? "primary" : ""} src={image.Url} alt={image.FileName} />)}
      </div>}
      <div className="info-grid">
        <div><span>Loại</span><strong>{asset.CategoryName || "—"}</strong></div><div><span>Công ty</span><strong>{asset.DepartmentName || "—"}</strong></div>
        <div><span>Người sử dụng</span><strong>{asset.AssignedTo || "Chưa cấp phát"}</strong></div><div><span>Địa chỉ IP</span><strong>{asset.IpAddress || "—"}</strong></div>
        <div><span>Serial</span><strong>{asset.SerialNumber || "—"}</strong></div><div><span>Ngày mua</span><strong>{asset.PurchaseDate ? new Date(asset.PurchaseDate).toLocaleDateString("vi-VN") : "—"}</strong></div>
      </div>
      <section className="public-section"><h2>Lịch sử cấp phát và thu hồi</h2>{assignments.length === 0 ? <p>Chưa có lịch sử.</p> : assignments.map((x, i) => <article key={i}><strong>{x.AssignedToSnapshot}</strong><p>Cấp phát: {new Date(x.AssignedDate).toLocaleDateString("vi-VN")}</p>{x.ReturnedDate ? <p>Thu hồi: {new Date(x.ReturnedDate).toLocaleDateString("vi-VN")}</p> : <span className="active-assignment">Đang sử dụng</span>}</article>)}</section>
      {isComputer && <section className="public-section"><h2>Phần mềm đang cài</h2>{software.length === 0 ? <p>Chưa có dữ liệu phần mềm.</p> : <div className="software-public-list">{software.map((x, i) => <div key={i}><strong>{x.SoftwareName}</strong><span>{x.Version || "Không rõ phiên bản"}{x.Publisher ? ` · ${x.Publisher}` : ""}</span></div>)}</div>}</section>}
    </div>
  </main>;
}
