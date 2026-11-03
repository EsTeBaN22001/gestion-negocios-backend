-- =============================================================================
-- SISTEMA DE GESTIÓN DE NEGOCIOS — SCRIPT INTEGRAL DE BASE DE DATOS
-- Motor: MySQL 8.0+ / MariaDB 10.4+
-- Codificación: UTF-8 (utf8mb4 / utf8mb4_unicode_ci)
--
-- INSTRUCCIONES DE IMPORTACIÓN:
-- 1. Desde phpMyAdmin / Laragon / HeidiSQL / DBeaver / MySQL Workbench:
--    - Abrir este archivo y ejecutarlo en su totalidad.
-- 2. Desde línea de comandos (Terminal / PowerShell / Bash):
--    mysql -u root -p < gestion_negocios.sql
--
-- NOTA: Este script crea la base de datos `gestion_negocios` (si no existe),
-- todas las tablas con sus relaciones 1:N, claves foráneas, índices de unicidad,
-- la tabla de control de Entity Framework Core (`__EFMigrationsHistory`),
-- y la carga completa de datos iniciales (Seed Masivo) para pruebas inmediatas.
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `gestion_negocios`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `gestion_negocios`;

-- Desactivar temporalmente chequeos de claves foráneas para permitir recarga limpia
SET FOREIGN_KEY_CHECKS = 0;
SET NAMES 'utf8mb4' COLLATE 'utf8mb4_unicode_ci';

-- =============================================================================
-- 1. CREACIÓN DE TABLA DE MIGRACIONES EF CORE (__EFMigrationsHistory)
-- =============================================================================
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- 2. TABLA: Sucursales
-- =============================================================================
DROP TABLE IF EXISTS `Sucursales`;
CREATE TABLE `Sucursales` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Direccion` varchar(200) CHARACTER SET utf8mb4 NULL,
    `Telefono` varchar(50) CHARACTER SET utf8mb4 NULL,
    `Activa` tinyint(1) NOT NULL DEFAULT 1,
    CONSTRAINT `PK_Sucursales` PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- 3. TABLA: Categorias
-- =============================================================================
DROP TABLE IF EXISTS `Categorias`;
CREATE TABLE `Categorias` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Descripcion` varchar(500) CHARACTER SET utf8mb4 NULL,
    `Activa` tinyint(1) NOT NULL DEFAULT 1,
    CONSTRAINT `PK_Categorias` PRIMARY KEY (`Id`),
    CONSTRAINT `IX_Categorias_Nombre` UNIQUE (`Nombre`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =============================================================================
-- 4. TABLA: Productos
-- =============================================================================
DROP TABLE IF EXISTS `Productos`;
CREATE TABLE `Productos` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Nombre` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `Descripcion` varchar(500) CHARACTER SET utf8mb4 NULL,
    `Codigo` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Precio` decimal(18,2) NOT NULL,
    `Activo` tinyint(1) NOT NULL DEFAULT 1,
    `CategoriaId` int NOT NULL,
    CONSTRAINT `PK_Productos` PRIMARY KEY (`Id`),
    CONSTRAINT `IX_Productos_Codigo` UNIQUE (`Codigo`),
    CONSTRAINT `FK_Productos_Categorias_CategoriaId` FOREIGN KEY (`CategoriaId`)
        REFERENCES `Categorias` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_Productos_CategoriaId` ON `Productos` (`CategoriaId`);

-- =============================================================================
-- 5. TABLA: Usuarios
-- =============================================================================
DROP TABLE IF EXISTS `Usuarios`;
CREATE TABLE `Usuarios` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Nombre` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Apellido` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Email` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Rol` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Avatar` varchar(255) CHARACTER SET utf8mb4 NULL,
    `SucursalId` int NULL,
    `Activo` tinyint(1) NOT NULL DEFAULT 1,
    `FechaCreacion` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT `PK_Usuarios` PRIMARY KEY (`Id`),
    CONSTRAINT `IX_Usuarios_Email` UNIQUE (`Email`),
    CONSTRAINT `FK_Usuarios_Sucursales_SucursalId` FOREIGN KEY (`SucursalId`)
        REFERENCES `Sucursales` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_Usuarios_SucursalId` ON `Usuarios` (`SucursalId`);

-- =============================================================================
-- 6. TABLA: Stocks
-- =============================================================================
DROP TABLE IF EXISTS `Stocks`;
CREATE TABLE `Stocks` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ProductoId` int NOT NULL,
    `SucursalId` int NOT NULL,
    `Cantidad` int NOT NULL DEFAULT 0,
    `StockMinimo` int NOT NULL DEFAULT 0,
    CONSTRAINT `PK_Stocks` PRIMARY KEY (`Id`),
    CONSTRAINT `IX_Stocks_ProductoId_SucursalId` UNIQUE (`ProductoId`, `SucursalId`),
    CONSTRAINT `FK_Stocks_Productos_ProductoId` FOREIGN KEY (`ProductoId`)
        REFERENCES `Productos` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Stocks_Sucursales_SucursalId` FOREIGN KEY (`SucursalId`)
        REFERENCES `Sucursales` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_Stocks_SucursalId` ON `Stocks` (`SucursalId`);

-- =============================================================================
-- 7. TABLA: Ventas
-- =============================================================================
DROP TABLE IF EXISTS `Ventas`;
CREATE TABLE `Ventas` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Fecha` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    `Total` decimal(18,2) NOT NULL,
    `SucursalId` int NOT NULL,
    `UsuarioId` int NOT NULL,
    `Estado` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Confirmada',
    `ArchivoAdjunto` varchar(255) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_Ventas` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Ventas_Sucursales_SucursalId` FOREIGN KEY (`SucursalId`)
        REFERENCES `Sucursales` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_Ventas_Usuarios_UsuarioId` FOREIGN KEY (`UsuarioId`)
        REFERENCES `Usuarios` (`Id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_Ventas_SucursalId` ON `Ventas` (`SucursalId`);
CREATE INDEX `IX_Ventas_UsuarioId` ON `Ventas` (`UsuarioId`);

-- =============================================================================
-- 8. TABLA: DetallesVenta
-- =============================================================================
DROP TABLE IF EXISTS `DetallesVenta`;
CREATE TABLE `DetallesVenta` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VentaId` int NOT NULL,
    `ProductoId` int NOT NULL,
    `Cantidad` int NOT NULL,
    `PrecioUnitario` decimal(18,2) NOT NULL,
    `Subtotal` decimal(18,2) NOT NULL,
    CONSTRAINT `PK_DetallesVenta` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_DetallesVenta_Productos_ProductoId` FOREIGN KEY (`ProductoId`)
        REFERENCES `Productos` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_DetallesVenta_Ventas_VentaId` FOREIGN KEY (`VentaId`)
        REFERENCES `Ventas` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE INDEX `IX_DetallesVenta_ProductoId` ON `DetallesVenta` (`ProductoId`);
CREATE INDEX `IX_DetallesVenta_VentaId` ON `DetallesVenta` (`VentaId`);

-- Reactivar chequeos de claves foráneas
SET FOREIGN_KEY_CHECKS = 1;

-- =============================================================================
-- 9. REGISTRO DE MIGRACIÓN INICIAL EF CORE
-- =============================================================================
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261006212234_InitialCreate', '8.0.13')
ON DUPLICATE KEY UPDATE `ProductVersion` = VALUES(`ProductVersion`);

-- =============================================================================
-- 10. DATOS INICIALES (SEED MASIVO DE DESARROLLO Y PRODUCCIÓN)
-- =============================================================================

-- SUCURSALES (4 sucursales representativas)
INSERT INTO `Sucursales` (`Id`, `Nombre`, `Direccion`, `Telefono`, `Activa`) VALUES
(1, 'Sucursal Centro', 'Av. Corrientes 1234, CABA', '011-4567-8900', 1),
(2, 'Sucursal Norte', 'Av. Cabildo 2500, Belgrano', '011-4789-1234', 1),
(3, 'Sucursal Sur', 'Av. Mitre 850, Avellaneda', '011-4201-5678', 1),
(4, 'Sucursal Oeste', 'Av. Rivadavia 14200, Ramos Mejía', '011-4658-9900', 0);

-- CATEGORÍAS
INSERT INTO `Categorias` (`Id`, `Nombre`, `Descripcion`, `Activa`) VALUES
(1, 'Bebidas', 'Aguas, refrescos, gaseosas y jugos naturales', 1),
(2, 'Almacén', 'Arroz, fideos, aceites, harinas y conservas', 1),
(3, 'Lácteos', 'Leches, quesos, yogures y derivados lácteos', 1),
(4, 'Limpieza', 'Detergentes, lavandinas, desinfectantes y cuidado del hogar', 1),
(5, 'Snacks y Golosinas', 'Chocolates, alfajores, galletitas dulces y saladas', 1),
(6, 'Bazar y Descartables', 'Vasos plásticos, servilletas y platos térmicos', 0);

-- USUARIOS
-- Contraseña para Administradores: Admin123*
-- Contraseña para Empleados:       Empleado123*
-- Hashes generados mediante PasswordHasher<Usuario> (ASP.NET Core Identity V3)
INSERT INTO `Usuarios` (`Id`, `Nombre`, `Apellido`, `Email`, `PasswordHash`, `Rol`, `Avatar`, `SucursalId`, `Activo`, `FechaCreacion`) VALUES
-- Administradores (SucursalId = NULL, gestión global)
(1, 'Administrador', 'Sistema', 'admin@gestionnegocios.local', 'AQAAAAIAAYagAAAAEH5pbNqAPg58mzfMTAzDUXOy8GmzKC+X8xDjoInBXFuqKUgiXTSzG4kpoPTpqMiMng==', 'Administrador', NULL, NULL, 1, NOW(6)),
(2, 'Laura', 'González', 'laura.admin@gestionnegocios.local', 'AQAAAAIAAYagAAAAEH5pbNqAPg58mzfMTAzDUXOy8GmzKC+X8xDjoInBXFuqKUgiXTSzG4kpoPTpqMiMng==', 'Administrador', NULL, NULL, 1, NOW(6)),

-- Empleados (SucursalId asignado obligatoriamente)
(3, 'Juan', 'Pérez', 'empleado@gestionnegocios.local', 'AQAAAAIAAYagAAAAEIwCdnu9A4sDdIAUtGoQIW0KODV5qMY2mTXdPCSkSonvwU/old/1VOVJg0Aq06bkfw==', 'Empleado', NULL, 1, 1, NOW(6)),
(4, 'Ana', 'Martínez', 'ana.norte@gestionnegocios.local', 'AQAAAAIAAYagAAAAEIwCdnu9A4sDdIAUtGoQIW0KODV5qMY2mTXdPCSkSonvwU/old/1VOVJg0Aq06bkfw==', 'Empleado', NULL, 2, 1, NOW(6)),
(5, 'Carlos', 'López', 'carlos.sur@gestionnegocios.local', 'AQAAAAIAAYagAAAAEIwCdnu9A4sDdIAUtGoQIW0KODV5qMY2mTXdPCSkSonvwU/old/1VOVJg0Aq06bkfw==', 'Empleado', NULL, 3, 1, NOW(6)),
(6, 'Mariana', 'Rossi', 'mariana.oeste@gestionnegocios.local', 'AQAAAAIAAYagAAAAEIwCdnu9A4sDdIAUtGoQIW0KODV5qMY2mTXdPCSkSonvwU/old/1VOVJg0Aq06bkfw==', 'Empleado', NULL, 4, 1, NOW(6)),
(7, 'Pedro', 'Inactivo', 'pedro.inactivo@gestionnegocios.local', 'AQAAAAIAAYagAAAAEIwCdnu9A4sDdIAUtGoQIW0KODV5qMY2mTXdPCSkSonvwU/old/1VOVJg0Aq06bkfw==', 'Empleado', NULL, 1, 0, NOW(6));

-- PRODUCTOS
INSERT INTO `Productos` (`Id`, `Nombre`, `Descripcion`, `Codigo`, `Precio`, `Activo`, `CategoriaId`) VALUES
-- Bebidas
(1, 'Agua Mineral sin Gas 1.5L', 'Agua pura de manantial en botella PET', 'BEB-001', 1200.00, 1, 1),
(2, 'Gaseosa Cola Clásica 2.25L', 'Bebida carbonatada refrescante sabor cola', 'BEB-002', 2800.00, 1, 1),
(3, 'Jugo de Naranja 100% Exprimido 1L', 'Jugo natural pasteurizado sin azúcar agregada', 'BEB-003', 2150.00, 1, 1),
(4, 'Cerveza Rubia Premium 473ml', 'Cerveza lager artesanal en lata', 'BEB-004', 1900.00, 1, 1),

-- Almacén
(5, 'Arroz Largo Fino 1kg', 'Grano seleccionado 00000 no se pasa', 'ALM-001', 1800.00, 1, 2),
(6, 'Fideos Tallarines al Huevo 500g', 'Pasta seca tradicional elaborada con sémola', 'ALM-002', 1350.00, 1, 2),
(7, 'Aceite de Girasol Puro 900ml', 'Aceite vegetal refinado rico en vitamina E', 'ALM-003', 2600.00, 1, 2),
(8, 'Atún al Natural en Lomitos 170g', 'Lomitos de atún claro en conserva de agua', 'ALM-004', 2900.00, 1, 2),

-- Lácteos
(9, 'Leche Entera Larga Vida 1L', 'Leche UAT fortificada con calcio y vitaminas A y D', 'LAC-001', 1400.00, 1, 3),
(10, 'Queso Cremoso Clásico 1kg', 'Queso blando ideal para pizzas y tartas', 'LAC-002', 7500.00, 1, 3),
(11, 'Yogur Firme de Frutilla 190g', 'Yogur entero endulzado con trozos de fruta', 'LAC-003', 980.00, 1, 3),

-- Limpieza
(12, 'Detergente Lavavajillas Limón 750ml', 'Ultra desengrasante con glicerina', 'LIM-001', 2100.00, 1, 4),
(13, 'Lavandina Concentrada 1L', 'Desinfectante multiuso acción antibacteriana', 'LIM-002', 1100.00, 1, 4),
(14, 'Jabón Líquido para Ropa 3L', 'Para ropa blanca y de color, rinde 30 lavados', 'LIM-003', 8900.00, 1, 4),

-- Snacks y Golosinas
(15, 'Alfajor Triple de Chocolate 70g', 'Relleno con abundante dulce de leche y baño de repostería', 'GOL-001', 1250.00, 1, 5),
(16, 'Papas Fritas Clásicas 140g', 'Papas crocantes saladas sin conservantes (Inactivo)', 'GOL-002', 2300.00, 0, 5);

-- STOCKS POR SUCURSAL
INSERT INTO `Stocks` (`Id`, `ProductoId`, `SucursalId`, `Cantidad`, `StockMinimo`) VALUES
-- Sucursal 1 (Centro)
(1, 1, 1, 50, 10),
(2, 2, 1, 40, 10),
(3, 3, 1, 4, 10),
(4, 4, 1, 0, 10),
(5, 5, 1, 60, 15),
(6, 6, 1, 30, 10),
(7, 7, 1, 3, 5),
(8, 8, 1, 20, 5),
(9, 9, 1, 35, 10),
(10, 10, 1, 0, 5),
(11, 11, 1, 18, 5),
(12, 12, 1, 25, 8),
(13, 13, 1, 40, 10),
(14, 14, 1, 15, 4),
(15, 15, 1, 5, 10),

-- Sucursal 2 (Norte)
(16, 1, 2, 30, 10),
(17, 2, 2, 2, 8),
(18, 5, 2, 0, 10),
(19, 6, 2, 45, 10),
(20, 9, 2, 20, 5),
(21, 13, 2, 25, 5),

-- Sucursal 3 (Sur)
(22, 1, 3, 25, 5),
(23, 7, 3, 12, 4),
(24, 8, 3, 18, 5),
(25, 9, 3, 2, 6),
(26, 12, 3, 0, 8),
(27, 14, 3, 15, 4);

-- VENTAS Y DETALLES DE VENTA
-- Venta 1: Sucursal Centro, Juan Pérez, Confirmada, Total: $5200.00
INSERT INTO `Ventas` (`Id`, `Fecha`, `Total`, `SucursalId`, `UsuarioId`, `Estado`, `ArchivoAdjunto`) VALUES
(1, DATE_SUB(NOW(6), INTERVAL 3 DAY), 5200.00, 1, 3, 'Confirmada', NULL);

INSERT INTO `DetallesVenta` (`Id`, `VentaId`, `ProductoId`, `Cantidad`, `PrecioUnitario`, `Subtotal`) VALUES
(1, 1, 1, 2, 1200.00, 2400.00),
(2, 1, 2, 1, 2800.00, 2800.00);

-- Venta 2: Sucursal Centro, Juan Pérez, Confirmada con comprobante, Total: $7550.00
INSERT INTO `Ventas` (`Id`, `Fecha`, `Total`, `SucursalId`, `UsuarioId`, `Estado`, `ArchivoAdjunto`) VALUES
(2, DATE_SUB(NOW(6), INTERVAL 2 DAY), 7550.00, 1, 3, 'Confirmada', 'ejemplo-factura-0001.pdf');

INSERT INTO `DetallesVenta` (`Id`, `VentaId`, `ProductoId`, `Cantidad`, `PrecioUnitario`, `Subtotal`) VALUES
(3, 2, 5, 2, 1800.00, 3600.00),
(4, 2, 6, 1, 1350.00, 1350.00),
(5, 2, 7, 1, 2600.00, 2600.00);

-- Venta 3: Sucursal Centro, Admin, Anulada (restaura stock y conserva histórico), Total: $2400.00
INSERT INTO `Ventas` (`Id`, `Fecha`, `Total`, `SucursalId`, `UsuarioId`, `Estado`, `ArchivoAdjunto`) VALUES
(3, DATE_SUB(NOW(6), INTERVAL 1 DAY), 2400.00, 1, 1, 'Anulada', NULL);

INSERT INTO `DetallesVenta` (`Id`, `VentaId`, `ProductoId`, `Cantidad`, `PrecioUnitario`, `Subtotal`) VALUES
(6, 3, 1, 2, 1200.00, 2400.00);

-- Venta 4: Sucursal Norte, Ana Martínez, Confirmada, Total: $4800.00
INSERT INTO `Ventas` (`Id`, `Fecha`, `Total`, `SucursalId`, `UsuarioId`, `Estado`, `ArchivoAdjunto`) VALUES
(4, DATE_SUB(NOW(6), INTERVAL 6 HOUR), 4800.00, 2, 4, 'Confirmada', NULL);

INSERT INTO `DetallesVenta` (`Id`, `VentaId`, `ProductoId`, `Cantidad`, `PrecioUnitario`, `Subtotal`) VALUES
(7, 4, 1, 4, 1200.00, 4800.00);

-- Venta 5: Sucursal Sur, Carlos López, Confirmada, Total: $2900.00
INSERT INTO `Ventas` (`Id`, `Fecha`, `Total`, `SucursalId`, `UsuarioId`, `Estado`, `ArchivoAdjunto`) VALUES
(5, NOW(6), 2900.00, 3, 5, 'Confirmada', NULL);

INSERT INTO `DetallesVenta` (`Id`, `VentaId`, `ProductoId`, `Cantidad`, `PrecioUnitario`, `Subtotal`) VALUES
(8, 5, 8, 1, 2900.00, 2900.00);
