﻿/*!
  Microsoft LightSwitch JavaScript Library v2.0.0 (for VS intellisense)
  Copyright (C) Microsoft Corporation. All rights reserved.
*/

/// <reference path="msls-2.0.0.js" />
// SIG // Begin signature block
// SIG // MIIQmAYJKoZIhvcNAQcCoIIQiTCCEIUCAQExCzAJBgUr
// SIG // DgMCGgUAMGcGCisGAQQBgjcCAQSgWTBXMDIGCisGAQQB
// SIG // gjcCAR4wJAIBAQQQEODJBs441BGiowAQS9NQkAIBAAIB
// SIG // AAIBAAIBAAIBADAhMAkGBSsOAwIaBQAEFOVjO9LNs430
// SIG // CUKGR0jqHrGF2RyvoIIOWzCCBBMwggNAoAMCAQICEGoL
// SIG // mU/AAEqrEd+K3OHgJ6owCQYFKw4DAh0FADB1MSswKQYD
// SIG // VQQLEyJDb3B5cmlnaHQgKGMpIDE5OTkgTWljcm9zb2Z0
// SIG // IENvcnAuMR4wHAYDVQQLExVNaWNyb3NvZnQgQ29ycG9y
// SIG // YXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUZXN0IFJv
// SIG // b3QgQXV0aG9yaXR5MB4XDTEwMDUxMDA3MDAwMFoXDTIw
// SIG // MTIyOTA3MDAwMFowcTELMAkGA1UEBhMCVVMxEzARBgNV
// SIG // BAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQx
// SIG // HjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEb
// SIG // MBkGA1UEAxMSTWljcm9zb2Z0IFRlc3QgUENBMIIBIjAN
// SIG // BgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA54KpNQYO
// SIG // Lv+3ZKN2BrbKb6YCq49u8J2Vi1lE94nsLJUeSmm56Yeb
// SIG // pRvh54mBWqbtY5f7g9JBRdMdvFM7zsN9wgvPVElql4xc
// SIG // lVIuw4pDu2tXazEyZWks0sRh4bHOBXBPcQQYlzMBg9JB
// SIG // iYLPZHWj75crIjwMoGboESa3Opw+6swyd5tH0eUFtVW0
// SIG // scVyrtYKIT5Qys0LGY+tPB5UQENJWUvaepPkkqG586xn
// SIG // 6vsP3qArVwHdM4YbPPevGHwM6baSJJNi4Bq9lotyCD0h
// SIG // 91L25uPzhlXonetdbol1v9pBwoRd2Fq/9AyavP29cPtH
// SIG // 2iUmBt8BmN8Gf+t1wS/rIBkPzQIDAQABo4HrMIHoMIGo
// SIG // BgNVHQEEgaAwgZ2AEMBjRdejAX15xXp6XyjbQ9ahdzB1
// SIG // MSswKQYDVQQLEyJDb3B5cmlnaHQgKGMpIDE5OTkgTWlj
// SIG // cm9zb2Z0IENvcnAuMR4wHAYDVQQLExVNaWNyb3NvZnQg
// SIG // Q29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBU
// SIG // ZXN0IFJvb3QgQXV0aG9yaXR5ghBf6k/S8h1DELboVD7Y
// SIG // lSYYMA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFFnv
// SIG // 0u3zsmSwmTTsxcIC5iqPij51MAsGA1UdDwQEAwIBhjAJ
// SIG // BgUrDgMCHQUAA4HBAKXom+KaNAGMXrmeZQAQHnveSdBM
// SIG // QvduzgTKzaqsDegPWGsbp7vIQdiS/nR3qzwo8qUHykXE
// SIG // 5lz+SH0K3SVmRMNm2PQXZmp/EeYiqMMbCWY1JNnanwkv
// SIG // NXYpHgCkGGrpyFfQr0d7qnTQL6O7ux8T433NKFUpW+Qh
// SIG // J42Abi1ZfHL/Qqqz/vEBsL/TTZThSlTxOUpUHQjudBGR
// SIG // FdxQedtDzRytfKhMV/hD9o72914dkX4N27G2ckvppT31
// SIG // NcjLd/WetDCCBOUwggPNoAMCAQICCmEhWnMAAQAAABgw
// SIG // DQYJKoZIhvcNAQEFBQAwcTELMAkGA1UEBhMCVVMxEzAR
// SIG // BgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
// SIG // bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
// SIG // bjEbMBkGA1UEAxMSTWljcm9zb2Z0IFRlc3QgUENBMB4X
// SIG // DTEwMDgwMjIxMzc0MloXDTE0MDgwMjIxMzc0MlowgYEx
// SIG // EzARBgoJkiaJk/IsZAEZFgNjb20xGTAXBgoJkiaJk/Is
// SIG // ZAEZFgltaWNyb3NvZnQxFDASBgoJkiaJk/IsZAEZFgRj
// SIG // b3JwMRcwFQYKCZImiZPyLGQBGRYHcmVkbW9uZDEgMB4G
// SIG // A1UEAxMXTVNJVCBUZXN0IENvZGVTaWduIENBIDIwggEi
// SIG // MA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCekFV5
// SIG // rCLvn5JjeS8QBhjfRtsbDYn79YFrM5XO03uQRR9J43OQ
// SIG // Qyn0FQ1scbJSXcuJRR+AyOuqsqdFMXvuFUqF7DMY3sfG
// SIG // +KrstLP+ly0cg1wGNARloJsOVmzglzsy4YjRX6VwiKCl
// SIG // sx541hS7GqxeyA3NEIov+CyTJOK0sIbetzIg9ouSI2+Q
// SIG // zBdHmSzGBcIHD4We5KuWndHdMhPLxwHGxxjWSsc2XyuT
// SIG // L+eck5x4//Y+i2hsVbM9VHtKZqdJqWTqA5JrZQurretL
// SIG // ODMJf/MxeTjJQIh6u8+mhnL7ZCFBoCsCj2ZMKh1grcwN
// SIG // HP1KV7/z3KcMEdRw4lOpiwiJ4A17AgMBAAGjggFsMIIB
// SIG // aDASBgkrBgEEAYI3FQEEBQIDAgACMCMGCSsGAQQBgjcV
// SIG // AgQWBBT+1EY9UtQQYUelkScYX/q+gO2GWjAdBgNVHQ4E
// SIG // FgQUDqGu6xW4BxsT7d4rIFNryDIpH+YwGQYJKwYBBAGC
// SIG // NxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8G
// SIG // A1UdEwEB/wQFMAMBAf8wHwYDVR0jBBgwFoAUWe/S7fOy
// SIG // ZLCZNOzFwgLmKo+KPnUwVwYDVR0fBFAwTjBMoEqgSIZG
// SIG // aHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwv
// SIG // cHJvZHVjdHMvTGVnYWN5VGVzdFBDQV8yMDEwLTA3LTEy
// SIG // LmNybDBbBggrBgEFBQcBAQRPME0wSwYIKwYBBQUHMAKG
// SIG // P2h0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kvY2Vy
// SIG // dHMvTGVnYWN5VGVzdFBDQV8yMDEwLTA3LTEyLmNydDAN
// SIG // BgkqhkiG9w0BAQUFAAOCAQEAv73zjBIpArrC9Fw8pvCO
// SIG // FaSN4yvsaMAJ3wvPT6pMtR+Jdh0/eyr1YQ/0s0TFV17e
// SIG // 3V5lTj2+ngYEGZQ71tFOBTZbBCeHhx8N0aj/V0OTJtbQ
// SIG // 7M4iCJ8duJ50btWdoU6DnO0ouZrAsRSvDjJYiV7uKI9J
// SIG // LKQeInUyynAqwNBZs9Tlw/6FdeNVregany+Upk4dEaIa
// SIG // mAe22cK+ir8y2RPgS1kGkaAFCezy9FJvBJghFkgEDlyi
// SIG // rTr4lWvOKmr2fqhiwgHSBQ3D/0WP/u/sbctPxdz680HC
// SIG // BsFHQXEHNosUQCvP8xjyn6C1lUoLE+oPxMEdW/H9q9aA
// SIG // plp9V/LtxcLbMDCCBVcwggQ/oAMCAQICChga27sAAgAz
// SIG // F0owDQYJKoZIhvcNAQEFBQAwgYExEzARBgoJkiaJk/Is
// SIG // ZAEZFgNjb20xGTAXBgoJkiaJk/IsZAEZFgltaWNyb3Nv
// SIG // ZnQxFDASBgoJkiaJk/IsZAEZFgRjb3JwMRcwFQYKCZIm
// SIG // iZPyLGQBGRYHcmVkbW9uZDEgMB4GA1UEAxMXTVNJVCBU
// SIG // ZXN0IENvZGVTaWduIENBIDIwHhcNMTMwMjA1MTYzMTMy
// SIG // WhcNMTQwMjA1MTYzMTMyWjAVMRMwEQYDVQQDEwpWUyBC
// SIG // bGQgTGFiMIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKB
// SIG // gQDNd1G1OyAwHpTTaJIiSOidpYHkreoNYH8KPiyMZjZV
// SIG // XROq1L0sZAEVqzBzHEoxQAcOHWM7OatCLb/clW557wKX
// SIG // SDhNKV36aoBjBNYXVb3hSXYh7oAcGSO7xnmtOHfgSnQb
// SIG // N6xPwMrHyGVYwvOau0UKQsil6IQZhT0k58xgfbGJfwID
// SIG // AQABo4ICvjCCArowCwYDVR0PBAQDAgeAMB0GA1UdDgQW
// SIG // BBQziYgw7MxML5Teg5lFFa6MS96XoTA9BgkrBgEEAYI3
// SIG // FQcEMDAuBiYrBgEEAYI3FQiDz4lNrfIChaGfDIL6yn2B
// SIG // 4ft0gU+CrrBqh/T9MgIBZAIBDDAfBgNVHSMEGDAWgBQO
// SIG // oa7rFbgHGxPt3isgU2vIMikf5jCB8QYDVR0fBIHpMIHm
// SIG // MIHjoIHgoIHdhjlodHRwOi8vY29ycHBraS9jcmwvTVNJ
// SIG // VCUyMFRlc3QlMjBDb2RlU2lnbiUyMENBJTIwMigyKS5j
// SIG // cmyGUGh0dHA6Ly9tc2NybC5taWNyb3NvZnQuY29tL3Br
// SIG // aS9tc2NvcnAvY3JsL01TSVQlMjBUZXN0JTIwQ29kZVNp
// SIG // Z24lMjBDQSUyMDIoMikuY3Jshk5odHRwOi8vY3JsLm1p
// SIG // Y3Jvc29mdC5jb20vcGtpL21zY29ycC9jcmwvTVNJVCUy
// SIG // MFRlc3QlMjBDb2RlU2lnbiUyMENBJTIwMigyKS5jcmww
// SIG // ga8GCCsGAQUFBwEBBIGiMIGfMEUGCCsGAQUFBzAChjlo
// SIG // dHRwOi8vY29ycHBraS9haWEvTVNJVCUyMFRlc3QlMjBD
// SIG // b2RlU2lnbiUyMENBJTIwMigyKS5jcnQwVgYIKwYBBQUH
// SIG // MAKGSmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kv
// SIG // bXNjb3JwL01TSVQlMjBUZXN0JTIwQ29kZVNpZ24lMjBD
// SIG // QSUyMDIoMikuY3J0MB8GA1UdJQQYMBYGCisGAQQBgjcK
// SIG // AwYGCCsGAQUFBwMDMCkGCSsGAQQBgjcVCgQcMBowDAYK
// SIG // KwYBBAGCNwoDBjAKBggrBgEFBQcDAzA6BgNVHREEMzAx
// SIG // oC8GCisGAQQBgjcUAgOgIQwfZGxhYkByZWRtb25kLmNv
// SIG // cnAubWljcm9zb2Z0LmNvbTANBgkqhkiG9w0BAQUFAAOC
// SIG // AQEAfZij/VYrCRfsZpWJmkQ4rBHXTe9jUhP3yip17V9u
// SIG // +6oaaxQGaAEyzhdg+jrYrSkfcgh0bKZXifNsrmFPKaAz
// SIG // mLr7esojrDf4X1Os4Uccb6xyuoZwCLFVmb5mrxbgN7eH
// SIG // X2R70UhHELFC2ok5SwC0AgeEpVURU7uPLrFXBYHE4B21
// SIG // Kwm/1PLs3Q8C2+l0RSGrKS/545jIp97wRfHDb7JL/Kvz
// SIG // qPuewUkZLGgcui/D+JSYiJvuLaJgB38TM5Gsdo2N8r63
// SIG // 0s6WyGkntFDmAhL4lnPH+tTb6qVRbLtUn9wgUhb7H1XW
// SIG // WPm1muLy7/tDVDBQJ/njfD/zP88KkG2WAKRFbjGCAakw
// SIG // ggGlAgEBMIGQMIGBMRMwEQYKCZImiZPyLGQBGRYDY29t
// SIG // MRkwFwYKCZImiZPyLGQBGRYJbWljcm9zb2Z0MRQwEgYK
// SIG // CZImiZPyLGQBGRYEY29ycDEXMBUGCgmSJomT8ixkARkW
// SIG // B3JlZG1vbmQxIDAeBgNVBAMTF01TSVQgVGVzdCBDb2Rl
// SIG // U2lnbiBDQSAyAgoYGtu7AAIAMxdKMAkGBSsOAwIaBQCg
// SIG // cDAQBgorBgEEAYI3AgEMMQIwADAZBgkqhkiG9w0BCQMx
// SIG // DAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3AgELMQ4wDAYK
// SIG // KwYBBAGCNwIBFTAjBgkqhkiG9w0BCQQxFgQUVO0KmRxH
// SIG // NT8tncM/7V3alZJmHgYwDQYJKoZIhvcNAQEBBQAEgYCa
// SIG // 8wTBURRjIwgJ3Pqq71Z1IK0vq1KsgPoUtj4dhbS29bLg
// SIG // wwi2T4omnsloHVRTS19R12QeaiXv6PSIw1MoQB45oM5s
// SIG // YGk4iGAwJAfU2qN5iNiCiMmw8Cs47DWMxdD+OgR520kV
// SIG // HrxWf9bb1zAqp34eavFONrcfEZ0qU9f7z4PjYQ==
// SIG // End signature block
